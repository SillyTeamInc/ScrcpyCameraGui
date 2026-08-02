using System.Diagnostics;
using ScrcpyCameraGui.Device;
using ScrcpyCameraGui.Render;
using ScrcpyCameraGui.ScreenCopy;
using SharpAdbClient;

namespace ScrcpyCameraGui;

public enum SessionState
{
    Stopped,
    Starting,
    Running,
    Reconnecting,
    Failed
}

public sealed class DeviceSession : IDisposable
{
    private static readonly TimeSpan ResumeRetryInterval = TimeSpan.FromSeconds(2);

    private readonly object _lock = new();
    private readonly V4l2SinkPool _sinkPool;
    private readonly ScrcpyConfigStore _configStore;
    private Process? _process;
    private EventHandler? _exitedHandler;
    private string? _sinkPath;

    private bool _resumeOnReconnect;
    private DateTimeOffset _lastResumeAttempt = DateTimeOffset.MinValue;

    public DeviceData Device { get; private set; }
    public SessionState State { get; private set; } = SessionState.Stopped;
    public string? SinkPath => _sinkPath;
    public string? LastError { get; private set; }
    public bool IsWireless => WirelessDevice.IsWireless(Device.Serial);
    public bool WasStreamingBeforeDisconnect => _resumeOnReconnect;

    public DeviceSession(DeviceData device, V4l2SinkPool sinkPool, ScrcpyConfigStore configStore)
    {
        Device = device;
        _sinkPool = sinkPool;
        _configStore = configStore;
    }

    public void UpdateDevice(DeviceData device)
    {
        lock (_lock)
        {
            Device = device;
        }
    }

    public void Start(bool silent = false)
    {
        lock (_lock)
        {
            if (State is SessionState.Running or SessionState.Starting) return;

            LastError = null;
            State = SessionState.Starting;

            try
            {
                _sinkPath = _sinkPool.Acquire();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                State = SessionState.Failed;
                if (!silent)
                    AlertDialogManager.Show($"No v4l2 sink for {Device.Name}", ex.Message, AlertLevel.Error);
                return;
            }

            var resetResult = V4l2LoopbackCtl.ResetCaps(_sinkPath);
            if (!resetResult.Success)
                Console.WriteLine($"Could not reset caps on {_sinkPath}: {resetResult.Error}");

            var options = new ScrcpyOptions
            {
                Serial = Device.Serial,
                VideoSource = VideoSource.Camera,
                V4l2Sink = _sinkPath,
                NoWindow = true
            };

            _configStore.Get(Device.Serial).ApplyTo(options);

            try
            {
                var process = Scrcpy.LaunchScrcpy(options);
                if (process == null)
                    throw new InvalidOperationException("Scrcpy.LaunchScrcpy returned null.");

                _process = process;
                process.EnableRaisingEvents = true;

                var handler = new EventHandler((_, _) => OnProcessExited());
                _exitedHandler = handler;
                process.Exited += handler;

                State = SessionState.Running;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                State = SessionState.Failed;
                ReleaseSinkLocked();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            DetachAndKillLocked();
            _resumeOnReconnect = false;
            CleanUpLocked();
            State = SessionState.Stopped;
        }
    }

    public void TickResume()
    {
        if (State != SessionState.Reconnecting || !_resumeOnReconnect) return;
        if (DateTimeOffset.UtcNow - _lastResumeAttempt < ResumeRetryInterval) return;

        _lastResumeAttempt = DateTimeOffset.UtcNow;
        Start(silent: true);

        lock (_lock)
        {
            if (State == SessionState.Failed && IsWireless)
                State = SessionState.Reconnecting;
        }
    }

    public void MarkDisconnected()
    {
        lock (_lock)
        {
            if (State == SessionState.Reconnecting) return;

            _resumeOnReconnect = State == SessionState.Running;
            DetachAndKillLocked();
            CleanUpLocked();
            State = SessionState.Reconnecting;
        }
    }

    public void Reconnected(DeviceData device)
    {
        UpdateDevice(device);

        lock (_lock)
        {
            if (State != SessionState.Reconnecting) return;
            if (!_resumeOnReconnect)
            {
                State = SessionState.Stopped;
                return;
            }
        }

        Start();
    }

    private void OnProcessExited()
    {
        lock (_lock)
        {
            if (State == SessionState.Running && IsWireless)
            {
                _resumeOnReconnect = true;
                State = SessionState.Reconnecting;
            }
            else if (State == SessionState.Running)
            {
                LastError = "scrcpy exited unexpectedly.";
                State = SessionState.Failed;
            }

            CleanUpLocked();
        }
    }

    private void DetachAndKillLocked()
    {
        if (_process == null) return;

        if (_exitedHandler != null)
        {
            _process.Exited -= _exitedHandler;
            _exitedHandler = null;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // nope
        }
    }

    private void CleanUpLocked()
    {
        _process?.Dispose();
        _process = null;
        ReleaseSinkLocked();
    }

    private void ReleaseSinkLocked()
    {
        if (_sinkPath == null) return;
        _sinkPool.Release(_sinkPath);
        _sinkPath = null;
    }

    public void Dispose() => Stop();
}