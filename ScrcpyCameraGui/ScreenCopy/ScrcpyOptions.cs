using System.Globalization;
using System.Reflection;

namespace ScrcpyCameraGui.ScreenCopy;

public sealed class ScrcpyOptions
{
    [ScrcpyArg("--serial")]
    public string? Serial { get; set; }

    [ScrcpyArg("--select-usb")]
    public bool SelectUsb { get; set; }

    [ScrcpyArg("--select-tcpip")]
    public bool SelectTcpIp { get; set; }

    [ScrcpyArg("--tcpip", StringMode = StringValueMode.NotNull)]
    public string? TcpIpAddress { get; set; } // value for --tcpip=<ip[:port]>
 
    [ScrcpyArg("--video-source")]
    public VideoSource? VideoSource { get; set; }

    [ScrcpyArg("--no-video")]
    public bool NoVideo { get; set; }

    [ScrcpyArg("--no-video-playback")]
    public bool NoVideoPlayback { get; set; }

    [ScrcpyArg("--video-codec-options")]
    public string? VideoCodecOptions { get; set; }

    [ScrcpyArg("--video-encoder")]
    public string? VideoEncoder { get; set; }

    [ScrcpyArg("--video-codec")]
    public VideoCodec? VideoCodec { get; set; }

    [ScrcpyArg("--video-bit-rate")]
    public string? VideoBitRate { get; set; } // e.g. "8M", "800K"

    [ScrcpyArg("--max-size")]
    public int? MaxSize { get; set; }

    [ScrcpyArg("--max-fps")]
    public int? MaxFps { get; set; }

    [ScrcpyArg("--video-buffer")]
    public int? VideoBufferMs { get; set; }
 
    [ScrcpyArg("--camera-facing")]
    public CameraFacing? CameraFacing { get; set; }

    [ScrcpyArg("--camera-id")]
    public string? CameraId { get; set; }

    [ScrcpyArg("--camera-size")]
    public string? CameraSize { get; set; } // "<width>x<height>"

    [ScrcpyArg("--camera-ar")]
    public string? CameraAspectRatio { get; set; } // "sensor", "4:3", "1.6"

    [ScrcpyArg("--camera-fps")]
    public int? CameraFps { get; set; }

    [ScrcpyArg("--camera-high-speed")]
    public bool CameraHighSpeed { get; set; }
 
    [ScrcpyArg("--no-audio")]
    public bool NoAudio { get; set; }

    [ScrcpyArg("--no-audio-playback")]
    public bool NoAudioPlayback { get; set; }

    [ScrcpyArg("--require-audio")]
    public bool RequireAudio { get; set; }

    [ScrcpyArg("--audio-codec")]
    public AudioCodec? AudioCodec { get; set; }

    [ScrcpyArg("--audio-source")]
    public string? AudioSource { get; set; } // output, playback, mic, etc.

    [ScrcpyArg("--audio-bit-rate")]
    public string? AudioBitRate { get; set; }

    [ScrcpyArg("--audio-buffer")]
    public int? AudioBufferMs { get; set; }

    [ScrcpyArg("--audio-dup")]
    public bool AudioDup { get; set; }
 
    [ScrcpyArg("--no-control")]
    public bool NoControl { get; set; }

    [ScrcpyArg("--keyboard")]
    public ControlMode? Keyboard { get; set; }

    [ScrcpyArg("--mouse")]
    public ControlMode? Mouse { get; set; }

    [ScrcpyArg("--gamepad")]
    public ControlMode? Gamepad { get; set; }

    [ScrcpyArg("--mouse-bind")]
    public string? MouseBind { get; set; }

    [ScrcpyArg("--otg")]
    public bool Otg { get; set; }
 
    [ScrcpyArg("--fullscreen")]
    public bool Fullscreen { get; set; }

    [ScrcpyArg("--always-on-top")]
    public bool AlwaysOnTop { get; set; }

    [ScrcpyArg("--window-borderless")]
    public bool WindowBorderless { get; set; }

    [ScrcpyArg("--window-title")]
    public string? WindowTitle { get; set; }

    [ScrcpyArg("--window-x")]
    public int? WindowX { get; set; }

    [ScrcpyArg("--window-y")]
    public int? WindowY { get; set; }

    [ScrcpyArg("--window-width")]
    public int? WindowWidth { get; set; }

    [ScrcpyArg("--window-height")]
    public int? WindowHeight { get; set; }

    [ScrcpyArg("--no-window")]
    public bool NoWindow { get; set; }
 
    [ScrcpyArg("--display-orientation")]
    public string? DisplayOrientation { get; set; } // 0/90/180/270, flipN, etc.

    [ScrcpyArg("--capture-orientation")]
    public string? CaptureOrientation { get; set; }

    [ScrcpyArg("--crop")]
    public string? Crop { get; set; } // width:height:x:y

    [ScrcpyArg("--angle")]
    public double? Angle { get; set; }
 
    [ScrcpyArg("--record")]
    public string? RecordFile { get; set; }

    [ScrcpyArg("--record-format")]
    public string? RecordFormat { get; set; }

    [ScrcpyArg("--record-orientation")]
    public string? RecordOrientation { get; set; }
 
    [ScrcpyArg("--disable-screensaver")]
    public bool DisableScreensaver { get; set; }

    [ScrcpyArg("--stay-awake")]
    public bool StayAwake { get; set; }

    [ScrcpyArg("--turn-screen-off")]
    public bool TurnScreenOffOnStart { get; set; }

    [ScrcpyArg("--power-off-on-close")]
    public bool PowerOffOnClose { get; set; }

    [ScrcpyArg("--no-power-on")]
    public bool NoPowerOn { get; set; }

    [ScrcpyArg("--kill-adb-on-close")]
    public bool KillAdbOnClose { get; set; }

    [ScrcpyArg("--pause-on-exit", StringMode = StringValueMode.NotNull)]
    public string? PauseOnExit { get; set; } // true/false/if-error, or null for flag-with-no-value

    [ScrcpyArg("--no-cleanup")]
    public bool NoCleanup { get; set; }

    [ScrcpyArg("--time-limit")]
    public double? TimeLimitSeconds { get; set; }

    [ScrcpyArg("--verbosity")]
    public LogLevel? Verbosity { get; set; }

    [ScrcpyArg("--print-fps")]
    public bool PrintFps { get; set; }
 
    [ScrcpyArg("--port")]
    public string? Port { get; set; } // "27183:27199"

    [ScrcpyArg("--tunnel-host")]
    public string? TunnelHost { get; set; }

    [ScrcpyArg("--tunnel-port")]
    public int? TunnelPort { get; set; }

    [ScrcpyArg("--force-adb-forward")]
    public bool ForceAdbForward { get; set; }

    [ScrcpyArg("--v4l2-buffer")]
    public int V4l2Buffer { get; set; }

    [ScrcpyArg("--v4l2-sink")]
    public string? V4l2Sink  { get; set; }
 
    public List<string> ExtraArguments { get; } = new();
 
    public List<string> ToArguments()
    {
        var args = BuildAttributedArguments();
        args.AddRange(ExtraArguments);

        return args;
    }

    private List<string> BuildAttributedArguments()
    {
        var args = new List<string>();

        foreach (PropertyInfo property in typeof(ScrcpyOptions)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .OrderBy(p => p.MetadataToken))
        {
            var attribute = property.GetCustomAttribute<ScrcpyArgAttribute>();
            if (attribute == null)
            {
                continue;
            }

            object? value = property.GetValue(this);
            if (value == null)
            {
                continue;
            }

            Type valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (valueType == typeof(bool))
            {
                if ((bool)value)
                {
                    args.Add(attribute.Flag);
                }
                continue;
            }

            if (valueType == typeof(string))
            {
                string stringValue = (string)value;
                bool include = attribute.StringMode == StringValueMode.NotNull
                    || !string.IsNullOrWhiteSpace(stringValue);

                if (include)
                {
                    args.Add($"{attribute.Flag}={stringValue}");
                }
                continue;
            }

            if (valueType == typeof(int))
            {
                args.Add($"{attribute.Flag}={((int)value).ToString(CultureInfo.InvariantCulture)}");
                continue;
            }

            if (valueType == typeof(double))
            {
                args.Add($"{attribute.Flag}={((double)value).ToString(CultureInfo.InvariantCulture)}");
                continue;
            }

            if (valueType.IsEnum)
            {
                args.Add($"{attribute.Flag}={ToKebab((Enum)value)}");
                continue;
            }

            throw new InvalidOperationException($"Unsupported Scrcpy argument property type: {property.Name} ({property.PropertyType.Name})");
        }
        
        // string.join makes me feel really stupid when i forget that it exists
        Console.WriteLine("parsed args: " + string.Join(" ", args));
        return args;
    }
 
    private static string ToKebab(Enum value)
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

    private enum StringValueMode
    {
        NonWhiteSpace,
        NotNull
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class ScrcpyArgAttribute(string flag) : Attribute
    {
        public string Flag { get; } = flag;
        public StringValueMode StringMode { get; set; } = StringValueMode.NonWhiteSpace;
    }
}