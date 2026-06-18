using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using AetherLink.Core.Abstractions;
using AetherLink.Core.Models;
using Microsoft.Extensions.Logging;

namespace AetherLink.UI.ViewModels;

/// <summary>
/// Primary ViewModel for the AetherLink main window.
/// Orchestrates device discovery, tunnel lifecycle and terminal spawning
/// through injected Core service interfaces. All state is observable via
/// data binding — the View contains zero business logic.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAndroidDeviceService    _deviceService;
    private readonly IAdbTunnelService        _tunnelService;
    private readonly ITerminalLauncherService _launcherService;
    private readonly IProxyRelayService       _proxyRelay;
    private readonly ILogger<MainViewModel>   _logger;

    // ─── Configuration ────────────────────────────────────────────────────────
    private const int    LocalProxyPort = 8888;
    private const string ProxyHost      = "127.0.0.1";

    // ─── Observable properties ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceSelected))]
    [NotifyCanExecuteChangedFor(nameof(ConnectTunnelCommand))]
    private AndroidDevice? _selectedDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonLabel))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonColor))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorColor))]
    [NotifyCanExecuteChangedFor(nameof(ScanDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectTunnelCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchTerminalCommand))]
    private TunnelState _currentState = TunnelState.Ready;

    [ObservableProperty]
    private string _statusMessage = "Ready. Press Scan to detect connected Android devices.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManualProxyLabel))]
    private string _manualUpstreamProxy = string.Empty;

    // ─── Derived properties ───────────────────────────────────────────────────

    public bool IsBusy        => CurrentState is TunnelState.Scanning;
    public bool IsConnected   => CurrentState is TunnelState.Connected;
    public bool IsDeviceSelected => SelectedDevice is not null;

    public string ManualProxyLabel =>
        string.IsNullOrWhiteSpace(ManualUpstreamProxy)
            ? "Auto-detect corporate proxy"
            : $"Upstream: {ManualUpstreamProxy}";

    public string ConnectButtonLabel => CurrentState switch
    {
        TunnelState.Connected => "⬛  Disconnect Tunnel",
        TunnelState.Scanning  => "⏳  Connecting…",
        TunnelState.Error     => "⚠  Retry Connection",
        _                     => "⚡  Connect Tunnel"
    };

    public Color ConnectButtonColor => CurrentState switch
    {
        TunnelState.Connected => Color.FromArgb("#E53935"),
        TunnelState.Error     => Color.FromArgb("#FB8C00"),
        TunnelState.Scanning  => Color.FromArgb("#546E7A"),
        _                     => Color.FromArgb("#00BFA5")
    };

    public Color StatusIndicatorColor => CurrentState switch
    {
        TunnelState.Connected => Color.FromArgb("#00E676"),
        TunnelState.Error     => Color.FromArgb("#FF5252"),
        TunnelState.Scanning  => Color.FromArgb("#FFD740"),
        _                     => Color.FromArgb("#78909C")
    };

    /// <summary>
    /// Live collection of devices displayed in the Picker.
    /// Refreshed in-place on each scan.
    /// </summary>
    public ObservableCollection<AndroidDevice> Devices { get; } = [];

    // ─── Constructor ──────────────────────────────────────────────────────────

    public MainViewModel(
        IAndroidDeviceService    deviceService,
        IAdbTunnelService        tunnelService,
        ITerminalLauncherService launcherService,
        IProxyRelayService       proxyRelay,
        ILogger<MainViewModel>   logger)
    {
        _deviceService   = deviceService;
        _tunnelService   = tunnelService;
        _launcherService = launcherService;
        _proxyRelay      = proxyRelay;
        _logger          = logger;
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the ADB server is running, then queries for connected devices.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanDevicesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User initiated device scan.");
        CurrentState  = TunnelState.Scanning;
        StatusMessage = "Starting ADB server and scanning for devices…";

        try
        {
            await _deviceService.EnsureServerRunningAsync(cancellationToken).ConfigureAwait(true);

            var devices = await _deviceService.GetConnectedDevicesAsync(cancellationToken)
                .ConfigureAwait(true);

            Devices.Clear();
            foreach (var device in devices)
                Devices.Add(device);

            if (Devices.Count == 0)
            {
                CurrentState  = TunnelState.Ready;
                StatusMessage = "No devices found. Connect your device and enable USB Debugging.";
            }
            else
            {
                SelectedDevice = Devices.FirstOrDefault(d => d.IsUsable) ?? Devices[0];
                CurrentState   = TunnelState.Ready;
                StatusMessage  = $"{Devices.Count} device(s) found. Select one and connect the tunnel.";
            }
        }
        catch (InvalidOperationException ex)
        {
            // ADB server could not start (binary missing).
            _logger.LogError(ex, "ADB server failed to start.");
            CurrentState  = TunnelState.Error;
            StatusMessage = ex.Message;
        }
        catch (OperationCanceledException)
        {
            CurrentState  = TunnelState.Ready;
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during scan.");
            CurrentState  = TunnelState.Error;
            StatusMessage = $"Scan error: {ex.Message}";
        }
    }

    private bool CanScan() => CurrentState is not TunnelState.Scanning;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles the full tethering session (proxy relay + ADB reverse + Android settings).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnectTunnel))]
    private async Task ConnectTunnelAsync(CancellationToken cancellationToken)
    {
        if (CurrentState is TunnelState.Connected)
        {
            await DisconnectTunnelAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

        if (SelectedDevice is null)
        {
            StatusMessage = "Select a device before connecting.";
            return;
        }

        if (!SelectedDevice.IsUsable)
        {
            CurrentState  = TunnelState.Error;
            StatusMessage = $"Device '{SelectedDevice.Serial}' is {SelectedDevice.Status}. " +
                            "Authorise USB Debugging on the device screen.";
            return;
        }

        // Propagate the manual upstream proxy before starting.
        _proxyRelay.ManualUpstreamProxy =
            string.IsNullOrWhiteSpace(ManualUpstreamProxy) ? null : ManualUpstreamProxy;

        _logger.LogInformation("Connecting tunnel for {Serial} on port {Port}.",
            SelectedDevice.Serial, LocalProxyPort);

        CurrentState  = TunnelState.Scanning;
        StatusMessage = "Establishing relay and ADB reverse tunnel…";

        try
        {
            await _tunnelService.StartTunnelAsync(
                    SelectedDevice.Serial,
                    LocalProxyPort,
                    cancellationToken)
                .ConfigureAwait(true);

            CurrentState  = TunnelState.Connected;
            StatusMessage = $"✔ Tunnel active — Android proxy → 127.0.0.1:{LocalProxyPort} " +
                            $"({SelectedDevice.DisplayLabel})";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Tunnel failed.");
            CurrentState  = TunnelState.Error;
            StatusMessage = ex.Message;
        }
        catch (OperationCanceledException)
        {
            CurrentState  = TunnelState.Ready;
            StatusMessage = "Connection cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while connecting.");
            CurrentState  = TunnelState.Error;
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private bool CanConnectTunnel() =>
        CurrentState is not TunnelState.Scanning &&
        (CurrentState is TunnelState.Connected || IsDeviceSelected);

    // ─────────────────────────────────────────────────────────────────────────

    private async Task DisconnectTunnelAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User requested tunnel disconnection.");
        StatusMessage = "Removing ADB reverse forward and clearing Android proxy…";

        try
        {
            await _tunnelService.StopTunnelAsync(cancellationToken).ConfigureAwait(true);
            CurrentState  = TunnelState.Ready;
            StatusMessage = "Tunnel disconnected. Android proxy settings cleared.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect.");
            CurrentState  = TunnelState.Ready;
            StatusMessage = $"Disconnect warning: {ex.Message} (state reset).";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a terminal with HTTP_PROXY / HTTPS_PROXY pointing to the local relay.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLaunchTerminal))]
    private async Task LaunchTerminalAsync(CancellationToken cancellationToken)
    {
        if (!_launcherService.IsPlatformSupported)
        {
            StatusMessage = "Terminal launch is not supported on this OS platform.";
            return;
        }

        try
        {
            int pid = await _launcherService.LaunchWithProxyAsync(ProxyHost, LocalProxyPort, cancellationToken)
                .ConfigureAwait(true);

            StatusMessage = pid > 0
                ? $"Terminal launched (PID {pid}). HTTP_PROXY=http://{ProxyHost}:{LocalProxyPort}"
                : "Terminal could not be started. Check application permissions.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Terminal launch cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch terminal.");
            StatusMessage = $"Launch error: {ex.Message}";
        }
    }

    private bool CanLaunchTerminal() => CurrentState is TunnelState.Connected;
}
