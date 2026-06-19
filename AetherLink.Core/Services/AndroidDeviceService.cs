using System.Runtime.InteropServices;
using AetherLink.Core.Abstractions;
using AetherLink.Core.Models;
using Microsoft.Extensions.Logging;
using SharpAdbClient;

namespace AetherLink.Core.Services;

/// <summary>
/// Concrete implementation of <see cref="IAndroidDeviceService"/>.
/// Uses <c>SharpAdbClient</c> to communicate with the ADB server over the
/// local socket — no shell parsing, no Regex fragility.
/// ADB binary resolution is ported from the Aether base project's <c>AdbManager.ResolveAdbPath()</c>,
/// giving priority to a locally bundled <c>adb/adb.exe</c> for full portability.
/// </summary>
public sealed class AndroidDeviceService(ILogger<AndroidDeviceService> logger) : IAndroidDeviceService
{
    private readonly AdbClient _adbClient = new();

    /// <inheritdoc/>
    public async Task EnsureServerRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Check if ADB is already running and responding on port 5037
            try
            {
                var status = _adbClient.GetAdbVersion();
                logger.LogInformation("ADB Server already running (Version {Version}).", status);
                return;
            }
            catch
            {
                logger.LogInformation("ADB server not responding on port 5037. Attempting to start it.");
            }

            string adbPath = ResolveAdbPath();
            logger.LogInformation("Starting ADB server. Binary: {Path}", adbPath);

            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = "start-server",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null) throw new InvalidOperationException("Failed to start adb process.");

            // Wait with a strict timeout to prevent zombie processes
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            var exitTask = process.WaitForExitAsync(cancellationToken);

            if (await Task.WhenAny(exitTask, timeoutTask) == timeoutTask)
            {
                logger.LogWarning("ADB start-server timed out. Killing process to prevent zombie accumulation.");
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("ADB server failed to start within 5 seconds.");
            }

            logger.LogInformation("ADB server started successfully.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to start ADB server. Check that the ADB binary exists.");
            throw new InvalidOperationException(
                "Could not start the ADB server. " +
                "Ensure the 'adb' folder with adb.exe is in the application directory.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AndroidDevice>> GetConnectedDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await Task.Run(
                () => _adbClient.GetDevices(),
                cancellationToken).ConfigureAwait(false);

            logger.LogDebug("SharpAdbClient returned {Count} device(s).", devices.Count);

            return devices
                .Select(MapToAndroidDevice)
                .ToList()
                .AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate devices. Is the ADB server running?");
            return [];
        }
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private static AndroidDevice MapToAndroidDevice(DeviceData data)
    {
        DeviceStatus status = data.State switch
        {
            DeviceState.Online       => DeviceStatus.Online,
            DeviceState.Unauthorized => DeviceStatus.Unauthorized,
            DeviceState.Offline      => DeviceStatus.Offline,
            _                        => DeviceStatus.Unknown
        };

        return new AndroidDevice(
            Serial: data.Serial,
            Status: status,
            Model: string.IsNullOrWhiteSpace(data.Model) ? null : data.Model);
    }

    /// <summary>
    /// Resolves the ADB binary path using the same priority chain as the Aether base project:
    /// 1. Bundled <c>adb/adb.exe</c> in the application directory (portable, highest priority).
    /// 2. Each directory in the <c>PATH</c> environment variable.
    /// 3. Common Android SDK install locations for Windows and macOS.
    /// 4. Bare filename fallback (relies on OS PATH at runtime).
    /// </summary>
    private static string ResolveAdbPath()
    {
        // Priority 0 — Bundled adb next to the executable (portable deployment).
        string baseDir   = AppDomain.CurrentDomain.BaseDirectory;
        string localAdb  = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(baseDir, "adb", "adb.exe")
            : Path.Combine(baseDir, "adb", "adb");

        if (File.Exists(localAdb))
            return localAdb;

        // Priority 1 — Search PATH entries.
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string adbName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "adb.exe" : "adb";

        foreach (string dir in pathEnv.Split(Path.PathSeparator))
        {
            string candidate = Path.Combine(dir, adbName);
            if (File.Exists(candidate))
                return candidate;
        }

        // Priority 2 — Common SDK locations.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] windowsPaths =
            [
                Path.Combine(localAppData, @"Android\Sdk\platform-tools\adb.exe"),
                @"C:\adb\adb.exe"
            ];

            foreach (string p in windowsPaths)
                if (File.Exists(p)) return p;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string macPath = Path.Combine(home, "Library", "Android", "sdk", "platform-tools", "adb");
            if (File.Exists(macPath)) return macPath;
        }

        // Priority 3 — Bare fallback; SharpAdbClient will surface a clear error if it fails.
        return adbName;
    }
}
