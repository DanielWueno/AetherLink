namespace AetherLink.Core.Abstractions;

/// <summary>
/// Defines the contract for orchestrating the ADB reverse-tunnel lifecycle.
/// Uses SharpAdbClient internally to create and remove ADB reverse forwards,
/// and injects the Android system proxy settings so all device traffic is captured.
/// </summary>
public interface IAdbTunnelService
{
    /// <summary>Gets a value indicating whether a tunnel session is currently active.</summary>
    bool IsForwardActive { get; }

    /// <summary>
    /// Gets the serial identifier of the device that owns the current active tunnel,
    /// or <c>null</c> if no tunnel is established.
    /// </summary>
    string? ActiveDeviceSerial { get; }

    /// <summary>
    /// Establishes a full tethering session for the given device:
    /// <list type="number">
    ///   <item>Starts the local proxy relay on <paramref name="localProxyPort"/>.</item>
    ///   <item>Creates an ADB reverse tunnel: device:<paramref name="localProxyPort"/> → host:<paramref name="localProxyPort"/>.</item>
    ///   <item>Injects Android system proxy settings so all device traffic flows through the relay.</item>
    /// </list>
    /// </summary>
    /// <param name="deviceSerial">ADB serial of the target device.</param>
    /// <param name="localProxyPort">The local TCP port where the proxy relay is listening.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task StartTunnelAsync(
        string deviceSerial,
        int localProxyPort,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tears down the active session cleanly:
    /// removes the Android system proxy, removes ADB reverse forwards, stops the relay.
    /// Safe to call when no tunnel is active.
    /// </summary>
    Task StopTunnelAsync(CancellationToken cancellationToken = default);
}
