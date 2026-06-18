using AetherLink.Core.Models;

namespace AetherLink.Core.Abstractions;

/// <summary>
/// Defines the contract for detecting and listing Android devices via SharpAdbClient.
/// Abstracts SharpAdbClient.DeviceData into the AetherLink domain model.
/// </summary>
public interface IAndroidDeviceService
{
    /// <summary>
    /// Asynchronously retrieves all Android devices currently visible to the ADB server.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of <see cref="AndroidDevice"/> records mapped from
    /// <c>SharpAdbClient.DeviceData</c>. Returns an empty collection if the ADB
    /// server is not running or no devices are connected.
    /// </returns>
    Task<IReadOnlyList<AndroidDevice>> GetConnectedDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the ADB server process is running on the host.
    /// Starts it from the bundled or SDK-resolved binary if it is not already active.
    /// </summary>
    Task EnsureServerRunningAsync(CancellationToken cancellationToken = default);
}
