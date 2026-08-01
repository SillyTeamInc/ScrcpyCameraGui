using System.Diagnostics;
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

    private readonly V4l2SinkPool _sinkPool;
    private Process? _process;
    private string? _sinkPath;

    private bool _resumeOnReconnect;
    private DateTimeOffset _lastResumeAttempt = DateTimeOffset.MinValue;

    public DeviceData Device { get; private set; }
    public SessionState State { get; private set; } = SessionState.Stopped;
    public string? SinkPath => _sinkPath;
    public string? LastError { get; private set; }
    public bool IsWireless => WirelessDevice.IsWireless(Device.Serial);
    public bool WasStreamingBeforeDisconnect => _resumeOnReconnect;

    public DeviceSession(DeviceData device, V4l2SinkPool sinkPool)
    {
        Device = device;
        _sinkPool = sinkPool;
    }

    public void UpdateDevice(DeviceData device) => Device = device;
    
    public void Start(bool silent = false)
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

        var options = new ScrcpyOptions
        {
            Serial = Device.Serial,
            VideoSource = VideoSource.Camera,
            CameraSize = "1920x1080",
            CameraFps = 60,
            NoAudio = true,
            V4l2Buffer = 8,
            V4l2Sink = _sinkPath,
            NoWindow = true
        };

        try
        {
            _process = Scrcpy.LaunchScrcpy(options);
            if (_process == null)
                throw new InvalidOperationException("Scrcpy.LaunchScrcpy returned null.");

            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) => OnProcessExited();
            State = SessionState.Running;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = SessionState.Failed;
            ReleaseSink();
        }
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // nop
        }

        _resumeOnReconnect = false;
        CleanUp();
        State = SessionState.Stopped;
    }

    public void TickResume()
    {
        if (State != SessionState.Reconnecting || !_resumeOnReconnect) return;
        if (DateTimeOffset.UtcNow - _lastResumeAttempt < ResumeRetryInterval) return;

        _lastResumeAttempt = DateTimeOffset.UtcNow;
        Start(silent: true);

        if (State == SessionState.Failed && IsWireless)
            State = SessionState.Reconnecting;
    }

    public void MarkDisconnected()
    {
        if (State == SessionState.Reconnecting) return;

        _resumeOnReconnect = State == SessionState.Running;

        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); }
            catch { /* best effort */ }
        }

        CleanUp();
        State = SessionState.Reconnecting;
    }

    public void Reconnected(DeviceData device)
    {
        UpdateDevice(device);

        if (State != SessionState.Reconnecting) return;
        if (!_resumeOnReconnect) { State = SessionState.Stopped; return; }

        Start();
    }

    private void OnProcessExited()
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

        CleanUp();
    }

    private void CleanUp()
    {
        _process?.Dispose();
        _process = null;
        ReleaseSink();
    }

    private void ReleaseSink()
    {
        if (_sinkPath == null) return;
        _sinkPool.Release(_sinkPath);
        _sinkPath = null;
    }

    public void Dispose() => Stop();
}
