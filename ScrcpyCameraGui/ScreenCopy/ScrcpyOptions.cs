using System.Globalization;

namespace ScrcpyCameraGui.ScreenCopy;

public sealed class ScrcpyOptions
{
    public string? Serial { get; set; }
    public bool SelectUsb { get; set; }
    public bool SelectTcpIp { get; set; }
    public string? TcpIpAddress { get; set; } // value for --tcpip=<ip[:port]>
 
    public VideoSource? VideoSource { get; set; }
    public bool NoVideo { get; set; }
    public bool NoVideoPlayback { get; set; }
    public string? VideoCodecOptions { get; set; }
    public string? VideoEncoder { get; set; }
    public VideoCodec? VideoCodec { get; set; }
    public string? VideoBitRate { get; set; } // e.g. "8M", "800K"
    public int? MaxSize { get; set; }
    public int? MaxFps { get; set; }
    public int? VideoBufferMs { get; set; }
 
    public CameraFacing? CameraFacing { get; set; }
    public string? CameraId { get; set; }
    public string? CameraSize { get; set; } // "<width>x<height>"
    public string? CameraAspectRatio { get; set; } // "sensor", "4:3", "1.6"
    public int? CameraFps { get; set; }
    public bool CameraHighSpeed { get; set; }
 
    public bool NoAudio { get; set; }
    public bool NoAudioPlayback { get; set; }
    public bool RequireAudio { get; set; }
    public AudioCodec? AudioCodec { get; set; }
    public string? AudioSource { get; set; } // output, playback, mic, etc.
    public string? AudioBitRate { get; set; }
    public int? AudioBufferMs { get; set; }
    public bool AudioDup { get; set; }
 
    public bool NoControl { get; set; }
    public ControlMode? Keyboard { get; set; }
    public ControlMode? Mouse { get; set; }
    public ControlMode? Gamepad { get; set; }
    public string? MouseBind { get; set; }
    public bool Otg { get; set; }
 
    public bool Fullscreen { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool WindowBorderless { get; set; }
    public string? WindowTitle { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
    public bool NoWindow { get; set; }
 
    public string? DisplayOrientation { get; set; } // 0/90/180/270, flipN, etc.
    public string? CaptureOrientation { get; set; }
    public string? Crop { get; set; } // width:height:x:y
    public double? Angle { get; set; }
 
    public string? RecordFile { get; set; }
    public string? RecordFormat { get; set; }
    public string? RecordOrientation { get; set; }
 
    public bool DisableScreensaver { get; set; }
    public bool StayAwake { get; set; }
    public bool TurnScreenOffOnStart { get; set; }
    public bool PowerOffOnClose { get; set; }
    public bool NoPowerOn { get; set; }
    public bool KillAdbOnClose { get; set; }
    public string? PauseOnExit { get; set; } // true/false/if-error, or null for flag-with-no-value
    public bool NoCleanup { get; set; }
    public double? TimeLimitSeconds { get; set; }
    public LogLevel? Verbosity { get; set; }
    public bool PrintFps { get; set; }
 
    public string? Port { get; set; } // "27183:27199"
    public string? TunnelHost { get; set; }
    public int? TunnelPort { get; set; }
    public bool ForceAdbForward { get; set; }
    public int V4l2Buffer { get; set; }
    public string? V4l2Sink  { get; set; }
 
    public List<string> ExtraArguments { get; } = new();
 
    public List<string> ToArguments()
    {
        var args = new List<string>();
 
        void Flag(bool condition, string flag)
        {
            if (condition)
            {
                args.Add(flag);
            }
        }
 
        void Value(string? value, string flag)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                args.Add($"{flag}={value}");
            }
        }
 
        void ValueInt(int? value, string flag)
        {
            if (value.HasValue)
            {
                args.Add($"{flag}={value.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }
 
        void ValueDouble(double? value, string flag)
        {
            if (value.HasValue)
            {
                args.Add($"{flag}={value.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }
 
        Value(Serial, "--serial");
        Flag(SelectUsb, "--select-usb");
        Flag(SelectTcpIp, "--select-tcpip");
        if (TcpIpAddress != null)
        {
            args.Add($"--tcpip={TcpIpAddress}");
        }
 
        if (VideoSource.HasValue)
        {
            args.Add($"--video-source={ToKebab(VideoSource.Value)}");
        }
        Flag(NoVideo, "--no-video");
        Flag(NoVideoPlayback, "--no-video-playback");
        Value(VideoCodecOptions, "--video-codec-options");
        Value(VideoEncoder, "--video-encoder");
        if (VideoCodec.HasValue)
        {
            args.Add($"--video-codec={ToKebab(VideoCodec.Value)}");
        }
        Value(VideoBitRate, "--video-bit-rate");
        ValueInt(MaxSize, "--max-size");
        ValueInt(MaxFps, "--max-fps");
        ValueInt(VideoBufferMs, "--video-buffer");
 
        if (CameraFacing.HasValue)
        {
            args.Add($"--camera-facing={ToKebab(CameraFacing.Value)}");
        }
        Value(CameraId, "--camera-id");
        Value(CameraSize, "--camera-size");
        Value(CameraAspectRatio, "--camera-ar");
        ValueInt(CameraFps, "--camera-fps");
        Flag(CameraHighSpeed, "--camera-high-speed");
 
        Flag(NoAudio, "--no-audio");
        Flag(NoAudioPlayback, "--no-audio-playback");
        Flag(RequireAudio, "--require-audio");
        if (AudioCodec.HasValue)
        {
            args.Add($"--audio-codec={ToKebab(AudioCodec.Value)}");
        }
        Value(AudioSource, "--audio-source");
        Value(AudioBitRate, "--audio-bit-rate");
        ValueInt(AudioBufferMs, "--audio-buffer");
        Flag(AudioDup, "--audio-dup");
 
        Flag(NoControl, "--no-control");
        if (Keyboard.HasValue)
        {
            args.Add($"--keyboard={ToKebab(Keyboard.Value)}");
        }
        if (Mouse.HasValue)
        {
            args.Add($"--mouse={ToKebab(Mouse.Value)}");
        }
        if (Gamepad.HasValue)
        {
            args.Add($"--gamepad={ToKebab(Gamepad.Value)}");
        }
        Value(MouseBind, "--mouse-bind");
        Flag(Otg, "--otg");
 
        Flag(Fullscreen, "--fullscreen");
        Flag(AlwaysOnTop, "--always-on-top");
        Flag(WindowBorderless, "--window-borderless");
        Value(WindowTitle, "--window-title");
        ValueInt(WindowX, "--window-x");
        ValueInt(WindowY, "--window-y");
        ValueInt(WindowWidth, "--window-width");
        ValueInt(WindowHeight, "--window-height");
        Flag(NoWindow, "--no-window");
 
        Value(DisplayOrientation, "--display-orientation");
        Value(CaptureOrientation, "--capture-orientation");
        Value(Crop, "--crop");
        ValueDouble(Angle, "--angle");

        Value(RecordFile, "--record");
        Value(RecordFormat, "--record-format");
        Value(RecordOrientation, "--record-orientation");
 
        Flag(DisableScreensaver, "--disable-screensaver");
        Flag(StayAwake, "--stay-awake");
        Flag(TurnScreenOffOnStart, "--turn-screen-off");
        Flag(PowerOffOnClose, "--power-off-on-close");
        Flag(NoPowerOn, "--no-power-on");
        Flag(KillAdbOnClose, "--kill-adb-on-close");
        if (PauseOnExit != null)
        {
            args.Add($"--pause-on-exit={PauseOnExit}");
        }
        Flag(NoCleanup, "--no-cleanup");
        ValueDouble(TimeLimitSeconds, "--time-limit");
        if (Verbosity.HasValue)
        {
            args.Add($"--verbosity={ToKebab(Verbosity.Value)}");
        }
        Flag(PrintFps, "--print-fps");
 
        Value(Port, "--port");
        Value(TunnelHost, "--tunnel-host");
        ValueInt(TunnelPort, "--tunnel-port");
        Flag(ForceAdbForward, "--force-adb-forward");
        ValueInt(V4l2Buffer, "--v4l2-buffer");
        Value(V4l2Sink, "--v4l2-sink");
 
        args.AddRange(ExtraArguments);
 
        return args;
    }
 
    private static string ToKebab<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        string name = value.ToString();

        var chars = new List<char>(name.Length * 2);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                chars.Add('-');
            }
            chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }
}