using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using AetherLink.Core.Abstractions;
using AetherLink.Core.Models;
using Microsoft.Extensions.Logging;

namespace AetherLink.UI.ViewModels;

/// <summary>
/// Enumerates the possible operational states of the AetherLink tunnel session.
/// Drives visual state changes in the View via DataTriggers.
/// </summary>
public enum TunnelState
{
    /// <summary>Application loaded, no action taken yet.</summary>
    Ready,

    /// <summary>Actively querying ADB for connected devices.</summary>
    Scanning,

    /// <summary>A forward tunnel is active and the proxy is live.</summary>
    Connected,

    /// <summary>An error occurred; inspect <see cref="MainViewModel.StatusMessage"/> for details.</summary>
    Error
}
