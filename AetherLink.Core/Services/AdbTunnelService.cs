using AetherLink.Core.Abstractions;
using AetherLink.Core.Models;
using Microsoft.Extensions.Logging;
using SharpAdbClient;

namespace AetherLink.Core.Services;

/// <summary>
/// Concrete implementation of <see cref="IAdbTunnelService"/>.
/// Orchestrates the full tethering lifecycle by composing
/// <see cref="IAndroidDeviceService"/> and <see cref="IProxyRelayService"/>.
///
/// Ported and elevated from <c>TetherService</c> in the Aether base project:
/// <list type="bullet">
///   <item>Uses SharpAdbClient for ADB operations (no shell calls).</item>
///   <item>Primary constructor + structured logging.</item>
///   <item>Thread-safe state management with <see cref="SemaphoreSlim"/>.</item>
///   <item>Clean async teardown without blocking <c>GetAwaiter().GetResult()</c>.</item>
/// </list>
/// </summary>
public sealed class AdbTunnelService(
    IProxyRelayService    proxyRelay,
    ILogger<AdbTunnelService> logger)
    : IAdbTunnelService, IDisposable
{
    private readonly AdbClient      _adbClient = new();
    private readonly SemaphoreSlim  _gate      = new(1, 1);

    private DeviceData? _activeDevice;
    private int         _activeProxyPort;

    // ─── Public state ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsForwardActive => _activeDevice is not null;

    /// <inheritdoc/>
    public string? ActiveDeviceSerial => _activeDevice?.Serial;

    // ─── Start ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StartTunnelAsync(
        string deviceSerial,
        int    localProxyPort,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceSerial, nameof(deviceSerial));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsForwardActive)
            {
                logger.LogWarning(
                    "Tunnel already active for {Serial}. Stopping it before starting a new one.",
                    _activeDevice!.Serial);
                await TeardownInternalAsync(CancellationToken.None).ConfigureAwait(false);
            }

            // 1. Resolve the target DeviceData from SharpAdbClient.
            var devices = await Task.Run(() => _adbClient.GetDevices(), cancellationToken)
                .ConfigureAwait(false);

            _activeDevice = devices.FirstOrDefault(d => d.Serial == deviceSerial)
                ?? throw new InvalidOperationException(
                    $"Device '{deviceSerial}' not found. Check USB connection and authorisation.");

            logger.LogInformation(
                "Starting tunnel — device: {Model} ({Serial}), proxy port: {Port}",
                _activeDevice.Model, _activeDevice.Serial, localProxyPort);

            _activeProxyPort = localProxyPort;

            // 2. Remove any stale forwards, then create a fresh one.
            //    ADB forward: host:localProxyPort → device:8080 (Every Proxy)
            await Task.Run(() =>
            {
                _adbClient.RemoveAllForwards(_activeDevice);
                _adbClient.CreateForward(
                    _activeDevice,
                    $"tcp:{localProxyPort}",
                    $"tcp:8080",
                    allowRebind: true);
            }, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("ADB forward created: host:tcp:{Port1} → device:tcp:8080", localProxyPort);

            logger.LogInformation("[AetherLink] Stealth tunnel active. PC proxy → Android:8080");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to start tunnel. Performing cleanup.");
            await TeardownInternalAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ─── Stop ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StopTunnelAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsForwardActive)
            {
                logger.LogDebug("StopTunnel called but no tunnel is active.");
                return;
            }

            await TeardownInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ─── Private teardown (called while gate is held) ────────────────────────

    private async Task TeardownInternalAsync(CancellationToken cancellationToken)
    {
        if (_activeDevice is null) return;

        // 1. Remove forward
        try
        {
            await Task.Run(() =>
            {
                _adbClient.RemoveAllForwards(_activeDevice);
            }, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("ADB forward removed for {Serial}.", _activeDevice.Serial);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not remove forward for {Serial} — device may have been disconnected.",
                _activeDevice.Serial);
        }

        _activeDevice    = null;
        _activeProxyPort = 0;

        logger.LogInformation("Tunnel torn down cleanly.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Synchronous disposal for IDisposable contract.
        // StopTunnelAsync is already guarded against double-calls.
        if (IsForwardActive)
        {
            try
            {
                if (_activeDevice is not null)
                {
                    _adbClient.RemoveAllForwards(_activeDevice);
                }
            }
            catch { /* Swallow during GC-driven disposal */ }
        }

        _gate.Dispose();
    }
}
