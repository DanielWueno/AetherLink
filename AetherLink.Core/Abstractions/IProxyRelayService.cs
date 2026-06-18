namespace AetherLink.Core.Abstractions;

/// <summary>
/// Defines the contract for the local TCP proxy relay that sits between
/// the ADB reverse tunnel and the upstream corporate proxy chain.
/// Implemented by <c>ProxyRelayService</c> (ported from Aether base project's <c>ProxyManager</c>).
/// </summary>
public interface IProxyRelayService : IDisposable
{
    /// <summary>Gets a value indicating whether the relay listener is currently active.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets or sets an explicit upstream proxy URI (e.g. <c>http://corp-proxy:8080</c>).
    /// When set, takes priority over all other proxy detection methods.
    /// When null or empty, the relay auto-detects the corporate proxy from Windows system
    /// settings and environment variables.
    /// </summary>
    string? ManualUpstreamProxy { get; set; }

    /// <summary>
    /// Starts the TCP relay listener on the specified local port.
    /// If the relay is already running, this call is a no-op.
    /// </summary>
    /// <param name="port">The local TCP port to bind (e.g. 8888).</param>
    void Start(int port);

    /// <summary>
    /// Stops the relay listener and cancels all in-flight connection tasks.
    /// Safe to call even if the relay is not running.
    /// </summary>
    void Stop();
}
