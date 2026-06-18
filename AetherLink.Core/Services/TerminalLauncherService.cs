using System.Diagnostics;
using System.Runtime.InteropServices;
using AetherLink.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace AetherLink.Core.Services;

/// <summary>
/// Cross-platform implementation of <see cref="ITerminalLauncherService"/>.
/// Spawns an interactive terminal session pre-configured with HTTP/S proxy
/// environment variables pointing to the active ADB tunnel loopback address.
/// </summary>
public sealed class TerminalLauncherService(ILogger<TerminalLauncherService> logger) : ITerminalLauncherService
{
    /// <inheritdoc/>
    public bool IsPlatformSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <inheritdoc/>
    public async Task<int> LaunchWithProxyAsync(
        string proxyHost,
        int proxyPort,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proxyHost, nameof(proxyHost));

        if (proxyPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(proxyPort), "Port must be between 1 and 65535.");

        if (!IsPlatformSupported)
        {
            logger.LogError("Terminal launch is not supported on this OS platform: {Platform}",
                RuntimeInformation.OSDescription);
            return -1;
        }

        string proxyUri = $"http://{proxyHost}:{proxyPort}";
        logger.LogInformation(
            "Launching terminal with proxy → HTTP_PROXY={ProxyUri} HTTPS_PROXY={ProxyUri}", proxyUri);

        ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? BuildWindowsStartInfo(proxyUri)
            : BuildMacOsStartInfo(proxyUri);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            bool started = process.Start();

            if (!started)
            {
                logger.LogError("Process.Start() returned false — the terminal could not be spawned.");
                return -1;
            }

            int pid = process.Id;
            logger.LogInformation("Terminal process started with PID {Pid}.", pid);

            // Detach immediately; the terminal should outlive this application.
            // We do not await WaitForExitAsync intentionally.
            await Task.CompletedTask.ConfigureAwait(false);

            return pid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to launch terminal process.");
            return -1;
        }
    }

    // ─── Platform-specific process configuration ────────────────────────────

    private static ProcessStartInfo BuildWindowsStartInfo(string proxyUri)
    {
        // Launch PowerShell with -NoExit so the window remains open.
        // We set the proxy variables at the process environment level so they
        // are inherited by every child process (e.g. curl, pip, npm) without
        // requiring the user to set them manually in the shell profile.
        var info = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = "-NoExit -NoLogo -Command \"Write-Host '[AetherLink] Proxy tunnel active.' -ForegroundColor Cyan\"",
            UseShellExecute = false,
            CreateNoWindow  = false,
        };

        InjectProxyVariables(info, proxyUri);
        return info;
    }

    private static ProcessStartInfo BuildMacOsStartInfo(string proxyUri)
    {
        // On macOS, open a new Terminal.app window running zsh with the proxy
        // variables exported in the shell environment before the interactive REPL.
        string exportCommands =
            $"export HTTP_PROXY={proxyUri}; " +
            $"export HTTPS_PROXY={proxyUri}; " +
            $"export http_proxy={proxyUri}; " +
            $"export https_proxy={proxyUri}; " +
            "echo '[AetherLink] Proxy tunnel active.'; " +
            "exec zsh";

        var info = new ProcessStartInfo
        {
            // `open -a Terminal` launches Terminal.app; we pass a shell command via `zsh -c`.
            FileName        = "/usr/bin/open",
            Arguments       = $"-a Terminal /usr/bin/zsh --args -c \"{exportCommands}\"",
            UseShellExecute = false,
            CreateNoWindow  = false,
        };

        InjectProxyVariables(info, proxyUri);
        return info;
    }

    private static void InjectProxyVariables(ProcessStartInfo info, string proxyUri)
    {
        // Injecting at ProcessStartInfo level ensures variables are set in the
        // OS-level process environment, fully transparent to corporate proxies
        // that inspect process metadata.
        info.EnvironmentVariables["HTTP_PROXY"]  = proxyUri;
        info.EnvironmentVariables["HTTPS_PROXY"] = proxyUri;
        info.EnvironmentVariables["http_proxy"]  = proxyUri;
        info.EnvironmentVariables["https_proxy"] = proxyUri;

        // NO_PROXY ensures loopback traffic is never re-routed.
        info.EnvironmentVariables["NO_PROXY"]    = "localhost,127.0.0.1,::1";
        info.EnvironmentVariables["no_proxy"]    = "localhost,127.0.0.1,::1";
    }
}
