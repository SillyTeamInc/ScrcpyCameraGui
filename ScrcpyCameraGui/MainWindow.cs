using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ScrcpyCameraGui.Device;
using ScrcpyCameraGui.Render;
using ScrcpyCameraGui.ScreenCopy;
using SharpAdbClient;

namespace ScrcpyCameraGui;

public sealed class MainWindow
{
    private readonly AdbService _adb = new();
    private readonly V4l2SinkPool _sinkPool = new();
    private readonly KnownWirelessDeviceStore _knownStore = new();
    private readonly ScrcpyConfigStore _configStore = new();
    private readonly Dictionary<string, DeviceSession> _sessions = new();
    private readonly Dictionary<string, DeviceRowWidget> _widgets = new();

    private readonly HashSet<string> _pairingInProgress = new();
    private readonly ConcurrentQueue<Action> _pendingActions = new();

    private readonly DeviceRowCallbacks _rowCallbacks;

    private bool _showDemoWindow;
    
    private List<string>? _missingDependencies;
    private bool _dependenciesChecked;

    private int _desiredV4l2DeviceCount = 4;
    private bool _persistV4l2Config = true;
    private bool _v4l2ApplyInProgress;

    public MainWindow()
    {
        _rowCallbacks = new DeviceRowCallbacks
        {
            EnableWireless = EnableWirelessForUsbDevice,
            IsPairing = s => _pairingInProgress.Contains(s.Device.Serial),
            Disconnect = DisconnectSession
        };

        _adb.DeviceConnected += OnDeviceConnected;
        _adb.DeviceDisconnected += OnDeviceDisconnected;
        _adb.Error += OnAdbError;

        if (V4l2ModuleManager.LoadedDeviceCount == 0)
        {
            AlertDialogManager.Show("v4l2loopback not detected",
                "No v4l2loopback devices were found. Scroll down to \"V4l2 loopback module\" to set it up.",
                AlertLevel.Warning);
        }

        if (!V4l2LoopbackCtl.IsAvailable())
        {
            AlertDialogManager.Show("v4l2loopback-ctl not found",
                "Install it for reliable resolution switching: sudo dnf install v4l2loopback-utils\n\n" +
                "Without it, changing a device's resolution can produce a corrupted image until the module is reloaded.",
                AlertLevel.Warning);
        }
    }

    public void Render()
    {
        while (_pendingActions.TryDequeue(out var action))
            action();

        // Check for missing dependencies on first frame
        if (!_dependenciesChecked)
        {
            _dependenciesChecked = true;
            _missingDependencies = DepUtil.CheckMissingDependencies();
        }

        // Show missing dependencies popup and exit
        if (_missingDependencies != null && _missingDependencies.Count > 0)
        {
            RenderMissingDependenciesPopup();
            return;
        }

        _adb.Tick();

        foreach (var session in _sessions.Values)
            session.TickResume();

        var io = ImGui.GetIO();

        if (!_showDemoWindow)
        {
            ImGui.SetNextWindowSize(io.DisplaySize, ImGuiCond.Always);
            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
            ImGui.Begin("scrcpy camera gui",
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoResize);
        }
        else
        {
            ImGui.Begin("scrcpy gui");
        }

        if (Debugger.IsAttached)
        {
            ImGui.Checkbox("Show Demo Window", ref _showDemoWindow);
            ImGui.SameLine();
            ImGui.Text($"{io.DisplaySize.X}x{io.DisplaySize.Y}");
        }
        
        ImGui.Separator();

        if (_widgets.Count == 0)
        {
            ImGui.TextDisabled("No devices found. Plug in a device with USB debugging enabled.");
        }
        else
        {
            foreach (var serial in _widgets.Keys.OrderBy(s => s))
                _widgets[serial].Render();
        }

        RenderKnownWirelessDevices();
        RenderV4l2ModulePanel();

        ImGui.Separator();
        if (ImGui.Button("Refresh devices"))
            _adb.Refresh();

        ImGui.End();

        AlertDialogManager.Render();

        if (_showDemoWindow)
            ImGui.ShowDemoWindow(ref _showDemoWindow);
    }

    private void RenderMissingDependenciesPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.Begin("Missing Dependencies", ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
        {
            ImGui.TextWrapped("The following dependencies are missing. Please install them and restart the application:");
            ImGui.Spacing();

            if (ImGui.BeginChild("Dependencies", new Vector2(0, -40), ImGuiChildFlags.Border))
            {
                foreach (var dep in _missingDependencies!)
                {
                    ImGui.BulletText(dep);
                }
                ImGui.EndChild();
            }

            ImGui.Spacing();
            if (ImGui.Button("Exit", new Vector2(120, 0)))
            {
                Environment.Exit(1);
            }

            ImGui.End();
        }
    }

    private void RenderKnownWirelessDevices()
    {
        var inactive = _knownStore.All
            .Where(k => !_sessions.ContainsKey(k.Serial))
            .OrderBy(k => k.Label)
            .ToList();

        if (inactive.Count == 0) return;

        ImGui.Spacing();
        ImGui.TextDisabled("Known wireless devices");
        ImGui.Separator();

        foreach (var known in inactive)
        {
            ImGui.PushID(known.Serial);
            ImGui.Text($"{known.Label}  ({known.Serial})");
            ImGui.SameLine();
            if (ImGui.Button("Connect"))
                _adb.ConnectWireless(known.Serial);
            ImGui.SameLine();
            if (ImGui.Button("Forget"))
                _knownStore.Forget(known.Serial);
            ImGui.PopID();
        }
    }

    private void OnDeviceConnected(DeviceData dev)
    {
        if (WirelessDevice.IsWireless(dev.Serial))
            _knownStore.Remember(dev.Serial, dev.Name);

        if (_sessions.TryGetValue(dev.Serial, out var existing))
        {
            existing.Reconnected(dev);
            return;
        }

        var session = new DeviceSession(dev, _sinkPool, _configStore);
        _sessions[dev.Serial] = session;
        _widgets[dev.Serial] = new DeviceRowWidget(session, _rowCallbacks, _configStore);
    }

    private void OnDeviceDisconnected(DeviceData dev)
    {
        if (!_sessions.TryGetValue(dev.Serial, out var session)) return;

        if (session.IsWireless)
        {
            session.MarkDisconnected();
            return;
        }

        session.Stop();
        session.Dispose();
        _sessions.Remove(dev.Serial);
        _widgets.Remove(dev.Serial);
    }

    private void DisconnectSession(DeviceSession session)
    {
        var serial = session.Device.Serial;

        _adb.Disconnect(serial);
        session.Stop();
        session.Dispose();
        _sessions.Remove(serial);
        _widgets.Remove(serial);

    }

    private void EnableWirelessForUsbDevice(DeviceSession usbSession)
    {
        var device = usbSession.Device;
        if (!_pairingInProgress.Add(device.Serial)) return;

        Task.Run(() =>
        {
            string? wirelessSerial = null;
            string? error = null;

            try
            {
                wirelessSerial = _adb.EnableWireless(device);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            _pendingActions.Enqueue(() =>
            {
                _pairingInProgress.Remove(device.Serial);

                if (wirelessSerial != null)
                {
                    _knownStore.Remember(wirelessSerial, device.Name);
                    AlertDialogManager.Show("Wireless enabled",
                        $"{device.Name} is now reachable wirelessly at {wirelessSerial}.",
                        AlertLevel.Info);
                }
                else
                {
                    AlertDialogManager.Show("Couldn't enable wireless",
                        error ?? "Check the console output for details.",
                        AlertLevel.Error);
                }
            });
        });
    }

    private void RenderV4l2ModulePanel()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("V4l2 loopback module");
        ImGui.Separator();

        var loadedCount = V4l2ModuleManager.LoadedDeviceCount;
        ImGui.Text(loadedCount > 0
            ? $"Currently loaded: {loadedCount} device(s)."
            : "Not currently loaded.");

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Device count", ref _desiredV4l2DeviceCount);
        if (_desiredV4l2DeviceCount < 1) _desiredV4l2DeviceCount = 1;
        if (_desiredV4l2DeviceCount > 16) _desiredV4l2DeviceCount = 16;

        ImGui.Checkbox("Persist across reboots", ref _persistV4l2Config);

        if (_v4l2ApplyInProgress)
        {
            ImGui.TextDisabled("Applying (you should get a prompt)");
        }
        else if (ImGui.Button("Apply"))
        {
            ApplyV4l2ModuleSettings();
        }
    }

    private void ApplyV4l2ModuleSettings()
    {
        if (_v4l2ApplyInProgress) return;
        _v4l2ApplyInProgress = true;

        var count = _desiredV4l2DeviceCount;
        var persist = _persistV4l2Config;

        Task.Run(() =>
        {
            var result = V4l2ModuleManager.Apply(count, persist);

            _pendingActions.Enqueue(() =>
            {
                _v4l2ApplyInProgress = false;

                if (result.Success)
                {
                    AlertDialogManager.Show("v4l2loopback updated",
                        $"Loaded with {count} device(s)" +
                        (persist ? ", and set to load automatically on boot." : "."),
                        AlertLevel.Info);
                }
                else
                {
                    AlertDialogManager.Show("Couldn't update v4l2loopback",
                        result.Error ?? "Unknown error.", AlertLevel.Error);
                }
            });
        });
    }

    private void OnAdbError(string message)
    {
        AlertDialogManager.Show("adb", message, AlertLevel.Warning);
    }

    public void Shutdown()
    {
        foreach (var session in _sessions.Values)
            session.Dispose();

        _sessions.Clear();
    }
}
