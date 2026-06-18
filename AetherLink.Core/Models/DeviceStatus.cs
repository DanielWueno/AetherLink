namespace AetherLink.Core.Models;

/// <summary>
/// Represents the connection state of an Android device as reported by SharpAdbClient.
/// Mirrors the values from <c>DeviceState</c> in SharpAdbClient mapped to AetherLink's domain.
/// </summary>
public enum DeviceStatus
{
    /// <summary>Device is connected and authorised for ADB communication.</summary>
    Online,

    /// <summary>Device is detected but awaiting USB debugging authorisation.</summary>
    Unauthorized,

    /// <summary>Device is listed but ADB cannot communicate with it.</summary>
    Offline,

    /// <summary>Device state could not be determined.</summary>
    Unknown
}
