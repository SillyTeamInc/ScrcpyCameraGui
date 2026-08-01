using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ScrcpyCameraGui.Render;
using SharpAdbClient;

namespace ScrcpyCameraGui;

public sealed class MainWindow
{
    private readonly AdbService _adb = new();
    private readonly V4l2SinkPool _sinkPool = new();
    private readonly KnownWirelessDeviceStore _knownStore = new();
    private readonly Dictionary<string, DeviceSession> _sessions = new();
    private readonly Dictionary<string, DeviceRowWidget> _widgets = new();

    private readonly HashSet<string> _pairingInProgress = new();
    private readonly ConcurrentQueue<Action> _pendingActions = new();

    private readonly DeviceRowCallbacks _rowCallbacks;

    private bool _showDemoWindow;

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
    }

    public void Render()
    {
        while (_pendingActions.TryDequeue(out var action))
            action();

        _adb.Tick();

        foreach (var session in _sessions.Values)
            session.TickResume();

        var io = ImGui.GetIO();

        if (!_showDemoWindow)
        {
            ImGui.SetNextWindowSize(io.DisplaySize, ImGuiCond.Always);
            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
            ImGui.Begin("scrcpy camera gui",
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBringToFrontOnFocus);
        }
        else
        {
            ImGui.Begin("scrcpy camera gui");
        }

        if (Debugger.IsAttached)
        {
            ImGui.Checkbox("Show Demo Window", ref _showDemoWindow);
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

        ImGui.Separator();
        if (ImGui.Button("Refresh devices"))
            _adb.Refresh();

        ImGui.End();

        AlertDialogManager.Render();

        if (_showDemoWindow)
            ImGui.ShowDemoWindow(ref _showDemoWindow);
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
            
            if (ImGui.Button("Connect")) _adb.ConnectWireless(known.Serial);
            ImGui.SameLine();
            if (ImGui.Button("Forget")) _knownStore.Forget(known.Serial);
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

        var session = new DeviceSession(dev, _sinkPool);
        _sessions[dev.Serial] = session;
        _widgets[dev.Serial] = new DeviceRowWidget(session, _rowCallbacks);
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
