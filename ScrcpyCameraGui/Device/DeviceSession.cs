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

/// <summary>
/// Owns the scrcpy process for a single device, plus the v4l2 sink it was
/// handed. One of these exists per connected device for its whole lifetime,
/// which is what gives us real multi-device support instead of the single
/// shared `_process` field the old code had.
///
/// All process lifecycle mutation goes through `_lock`. Process.Exited is
/// raised on a ThreadPool thread, and an intentional Stop()/MarkDisconnected()
/// can happen on the main thread at almost the same moment - without a lock,
/// both paths can end up disposing the same native Process handle
/// concurrently, which is the likely cause of native heap corruption
/// ("free(): invalid pointer") on close rather than a clean .NET exception.
/// </summary>
public sealed class DeviceSession : IDisposable
{
    private static readonly TimeSpan ResumeRetryInterval = TimeSpan.FromSeconds(2);

    private readonly object _lock = new();
    private readonly V4l2SinkPool _sinkPool;
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

    public DeviceSession(DeviceData device, V4l2SinkPool sinkPool)
    {
        Device = device;
        _sinkPool = sinkPool;
    }

    public void UpdateDevice(DeviceData device)
    {
        lock (_lock) { Device = device; }
    }

    /// <param name="silent">
    /// Suppresses the "no v4l2 sink" popup. Used by <see cref="TickResume"/>
    /// so a repeated automatic retry doesn't spam the same alert every
    /// 2 seconds - the inline error text on the row is enough there.
    /// </param>
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

    /// <summary>
    /// Called every frame by MainWindow while this session is Reconnecting.
    /// Retries launching scrcpy every 2 seconds entirely on its own - it does
    /// NOT wait for adb to report anything, because the base adb transport
    /// can stay "online" the whole time a wireless stream is actually dead
    /// (the camera/video connection is independent of the adb transport).
    /// </summary>
    public void TickResume()
    {
        if (State != SessionState.Reconnecting || !_resumeOnReconnect) return;
        if (DateTimeOffset.UtcNow - _lastResumeAttempt < ResumeRetryInterval) return;

        _lastResumeAttempt = DateTimeOffset.UtcNow;
        Start(silent: true);

        lock (_lock)
        {
            // Start() may have failed fast (device still unreachable, sink
            // still busy, etc). If so, and we're still trying to resume a
            // wireless session, go back to Reconnecting instead of
            // surfacing a hard failure - we'll just try again in 2 seconds.
            if (State == SessionState.Failed && IsWireless)
                State = SessionState.Reconnecting;
        }
    }

    /// <summary>
    /// Called by MainWindow when this device drops off adb's device list
    /// (a genuine transport-level disconnect, as opposed to the video/camera
    /// connection dying on its own - see OnProcessExited for that case).
    /// </summary>
    public void MarkDisconnected()
    {
        lock (_lock)
        {
            // OnProcessExited usually gets here first for wireless drops,
            // since scrcpy notices the dead connection faster than adb's
            // device list updates. Don't recompute the resume flag if we're
            // already in that state - it would wipe out the correct value.
            if (State == SessionState.Reconnecting) return;

            _resumeOnReconnect = State == SessionState.Running;
            DetachAndKillLocked();
            CleanUpLocked();
            State = SessionState.Reconnecting;
        }
    }

    /// <summary>
    /// Called by MainWindow when a previously-dropped wireless device shows
    /// back up in adb's device list. This is a fast path on top of
    /// TickResume - if adb genuinely lost and regained the transport, this
    /// resumes immediately instead of waiting for the next 2-second tick.
    /// </summary>
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
                // scrcpy notices a dead wireless connection and exits almost
                // immediately - well before adb's own device list would
                // catch up (if it ever does; the base adb transport often
                // stays "online" the whole time). Treat this exit itself as
                // the disconnect signal rather than waiting on AdbService.
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

    /// <summary>Removes the Exited subscription and kills the process, if any. Caller must hold _lock.</summary>
    private void DetachAndKillLocked()
    {
        if (_process == null) return;

        // Unsubscribe before killing so an intentional Stop()/MarkDisconnected()
        // can never race the async Exited callback trying to clean up the
        // same Process object from a different thread at the same time.
        if (_exitedHandler != null)
        {
            _process.Exited -= _exitedHandler;
            _exitedHandler = null;
        }

        try
        {
            if (!_process.HasExited)
            {
                // The old code called Process.Close(), which only releases
                // the .NET handle and does NOT terminate the process -
                // scrcpy kept running in the background even after clicking
                // "Stop".
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // Already exited or the platform refused the kill - either way
            // there's nothing more we can do here.
        }
    }

    /// <summary>Caller must hold _lock.</summary>
    private void CleanUpLocked()
    {
        _process?.Dispose();
        _process = null;
        ReleaseSinkLocked();
    }

    /// <summary>Caller must hold _lock.</summary>
    private void ReleaseSinkLocked()
    {
        if (_sinkPath == null) return;
        _sinkPool.Release(_sinkPath);
        _sinkPath = null;
    }

    public void Dispose() => Stop();
}
