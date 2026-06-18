namespace AetherLink.Core.Models;

/// <summary>
/// Immutable value-object representing a single Android device discovered by ADB.
/// Constructed from <c>SharpAdbClient.DeviceData</c> to decouple the domain model
/// from the third-party library type.
/// </summary>
/// <param name="Serial">
/// The unique ADB serial identifier (e.g. "emulator-5554", "R3CN904XXXX", or an IP:port pair).
/// </param>
/// <param name="Status">The current connection and authorisation status.</param>
/// <param name="Model">Optional product model name retrieved by SharpAdbClient.</param>
public sealed record AndroidDevice(
    string Serial,
    DeviceStatus Status,
    string? Model = null)
{
    /// <summary>
    /// A human-readable label suitable for display in a Picker or list control.
    /// Falls back to the serial when the model name is not available.
    /// </summary>
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Model)
            ? $"{Serial} [{Status}]"
            : $"{Model} — {Serial} [{Status}]";

    /// <summary>
    /// Indicates whether this device is ready to accept ADB commands.
    /// </summary>
    public bool IsUsable => Status is DeviceStatus.Online;
}
