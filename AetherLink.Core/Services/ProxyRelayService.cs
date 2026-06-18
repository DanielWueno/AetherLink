using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using AetherLink.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace AetherLink.Core.Services;

/// <summary>
/// Concrete implementation of <see cref="IProxyRelayService"/>.
/// A raw TCP relay that sits on a local port and chains inbound connections
/// to an upstream proxy — either the detected corporate proxy or a manually configured one.
///
/// This implementation is a direct port of <c>ProxyManager</c> from the Aether base project,
/// modernised with:
/// <list type="bullet">
///   <item>Structured logging via <see cref="ILogger{T}"/>.</item>
///   <item>File-scoped namespace and primary constructor.</item>
///   <item>Nullable annotations and C# 14 collection expressions.</item>
/// </list>
/// </summary>
public sealed class ProxyRelayService(ILogger<ProxyRelayService> logger)
    : IProxyRelayService
{
    private TcpListener?            _listener;
    private CancellationTokenSource? _cts;

    private static readonly Regex HostHeaderRegex = new(
        pattern: @"Host:\s*([^\r\n]+)",
        options: RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromSeconds(2));

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public string? ManualUpstreamProxy { get; set; }

    /// <inheritdoc/>
    public void Start(int port)
    {
        if (IsRunning)
        {
            logger.LogDebug("ProxyRelayService.Start called but relay is already running on port {Port}.", port);
            return;
        }

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _cts      = new CancellationTokenSource();
        IsRunning = true;

        // Accept loop runs on a dedicated thread-pool thread.
        _ = Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        logger.LogInformation("[AetherLink] Chained relay active on tcp://127.0.0.1:{Port}", port);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _listener?.Stop();
        IsRunning = false;
        logger.LogInformation("[AetherLink] Relay stopped.");
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    // ─── Connection accept loop ──────────────────────────────────────────────

    private async Task AcceptConnectionsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
                // Fire-and-forget; each connection is independent.
                _ = HandleChainedClientAsync(client, token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Accept loop error — relay may have stopped.");
                break;
            }
        }
    }

    // ─── Per-connection handler (ported from Aether ProxyManager) ───────────

    private async Task HandleChainedClientAsync(TcpClient client, CancellationToken token)
    {
        string host = "unknown";
        int    port = 0;

        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                // Read the initial HTTP/CONNECT request from the downstream caller.
                byte[] buffer    = new byte[8192];
                int    bytesRead = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (bytesRead <= 0) return;

                string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                bool   isHttps = request.StartsWith("CONNECT", StringComparison.OrdinalIgnoreCase);

                if (isHttps)
                {
                    // CONNECT host:port HTTP/1.1
                    string[] target = request.Split(' ')[1].Split(':');
                    host = target[0];
                    port = target.Length > 1 ? int.Parse(target[1]) : 443;
                }
                else
                {
                    Match match = HostHeaderRegex.Match(request);
                    if (match.Success)
                    {
                        string[] target = match.Groups[1].Value.Trim().Split(':');
                        host = target[0];
                        port = target.Length > 1 ? int.Parse(target[1]) : 80;
                    }
                }

                if (string.IsNullOrEmpty(host) || host == "unknown") return;

                Uri? upstreamProxy = ResolveUpstreamProxy(host, port, isHttps);

                using TcpClient targetClient = new();

                if (upstreamProxy is not null)
                {
                    // ── CHAINED mode: route through the upstream corporate proxy ──
                    await targetClient.ConnectAsync(upstreamProxy.Host, upstreamProxy.Port, token)
                        .ConfigureAwait(false);

                    using NetworkStream targetStream = targetClient.GetStream();

                    // Ask the upstream proxy to CONNECT to the final destination.
                    byte[] chainReq = Encoding.ASCII.GetBytes(
                        $"CONNECT {host}:{port} HTTP/1.1\r\n" +
                        $"Host: {host}:{port}\r\n" +
                        "Proxy-Connection: Keep-Alive\r\n\r\n");

                    await targetStream.WriteAsync(chainReq, token).ConfigureAwait(false);

                    // Read the upstream CONNECT response.
                    byte[] respBuf   = new byte[8192];
                    int    respBytes = await targetStream.ReadAsync(respBuf, token).ConfigureAwait(false);
                    string resp      = Encoding.ASCII.GetString(respBuf, 0, respBytes);

                    if (resp.Contains("200"))
                    {
                        logger.LogDebug("[Chain] TUNNELED via {UpstreamHost} → {Host}:{Port}",
                            upstreamProxy.Host, host, port);
                        await EstablishRelayAsync(stream, targetStream, isHttps, buffer, bytesRead, token)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        logger.LogWarning("[Chain] REJECTED by upstream proxy {UpstreamHost} for {Host}: {Response}",
                            upstreamProxy.Host, host, resp.Split("\r\n")[0]);
                    }
                }
                else
                {
                    // ── DIRECT mode: no upstream proxy detected ──
                    await targetClient.ConnectAsync(host, port, token).ConfigureAwait(false);
                    using NetworkStream targetStream = targetClient.GetStream();

                    logger.LogDebug("[Chain] DIRECT connection → {Host}:{Port}", host, port);
                    await EstablishRelayAsync(stream, targetStream, isHttps, buffer, bytesRead, token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("[Chain Error] {Host}:{Port} → {Message}", host, port, ex.Message);
        }
    }

    // ─── Upstream proxy resolution (priority chain from Aether base) ─────────

    private Uri? ResolveUpstreamProxy(string targetHost, int targetPort, bool isHttps)
    {
        // Priority 0 — Manual proxy configured from the UI.
        if (!string.IsNullOrWhiteSpace(ManualUpstreamProxy))
        {
            string raw = ManualUpstreamProxy.Trim();
            if (!raw.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                raw = "http://" + raw;
            return new Uri(raw);
        }

        // Priority 1 — Windows system proxy (IE / WinInet / Group Policy).
        IWebProxy systemProxy = WebRequest.GetSystemWebProxy();
        Uri targetUri = new($"{(isHttps ? "https" : "http")}://{targetHost}:{targetPort}");
        Uri proxyUri  = systemProxy.GetProxy(targetUri);
        if (proxyUri is not null && proxyUri != targetUri)
            return proxyUri;

        // Priority 2 — Environment variables (common in corporate Linux/macOS too).
        string envProxy = Environment.GetEnvironmentVariable("HTTP_PROXY")
                       ?? Environment.GetEnvironmentVariable("http_proxy")
                       ?? string.Empty;

        if (!string.IsNullOrEmpty(envProxy))
        {
            if (!envProxy.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                envProxy = "http://" + envProxy;
            return new Uri(envProxy);
        }

        return null; // No upstream proxy found — use direct connection.
    }

    // ─── Bidirectional stream relay ──────────────────────────────────────────

    private static async Task EstablishRelayAsync(
        NetworkStream clientStream,
        NetworkStream targetStream,
        bool          isHttps,
        byte[]        initialBuffer,
        int           initialBytes,
        CancellationToken token)
    {
        if (isHttps)
        {
            // Signal the downstream caller that the CONNECT tunnel is ready.
            byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
            await clientStream.WriteAsync(ok, token).ConfigureAwait(false);
        }
        else
        {
            // Forward the already-read HTTP request bytes to the upstream.
            await targetStream.WriteAsync(initialBuffer.AsMemory(0, initialBytes), token)
                .ConfigureAwait(false);
        }

        // Bidirectional copy — complete when either side closes.
        Task t1 = clientStream.CopyToAsync(targetStream, token);
        Task t2 = targetStream.CopyToAsync(clientStream, token);
        await Task.WhenAny(t1, t2).ConfigureAwait(false);
    }
}
