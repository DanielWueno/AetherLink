namespace AetherLink.Core.Abstractions;

/// <summary>
/// Defines the contract for launching an interactive terminal process
/// with injected proxy environment variables.
/// </summary>
public interface ITerminalLauncherService
{
    /// <summary>
    /// Launches a new interactive terminal window (PowerShell on Windows, zsh on macOS)
    /// with the <c>HTTP_PROXY</c> and <c>HTTPS_PROXY</c> environment variables pre-configured
    /// to route traffic through the active ADB tunnel.
    /// </summary>
    /// <param name="proxyHost">The host address of the local proxy endpoint (e.g. "127.0.0.1").</param>
    /// <param name="proxyPort">The local port that the ADB forward is bound to (e.g. 1080).</param>
    /// <param name="cancellationToken">Token to cancel the launch operation.</param>
    /// <returns>The OS process ID of the spawned terminal, or <c>-1</c> if launch failed.</returns>
    Task<int> LaunchWithProxyAsync(
        string proxyHost,
        int proxyPort,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the current OS platform is supported for terminal launching.
    /// </summary>
    bool IsPlatformSupported { get; }
}
