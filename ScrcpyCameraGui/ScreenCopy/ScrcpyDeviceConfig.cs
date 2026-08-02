namespace ScrcpyCameraGui.ScreenCopy;

public sealed class ScrcpyDeviceConfig
{
    public VideoCodec VideoCodec { get; set; } = VideoCodec.H264;
    public string VideoBitRate { get; set; } = "8M";
    public CameraFacing CameraFacing { get; set; } = CameraFacing.Back;
    public string CameraSize { get; set; } = "1920x1080";
    public int CameraFps { get; set; } = 60;
    public bool CameraHighSpeed { get; set; }
    
    public int V4l2BufferMs { get; set; } = 0;

    public bool NoAudio { get; set; } = true;

    public string ExtraArguments { get; set; } = "";

    public static ScrcpyDeviceConfig Default() => new();

    public ScrcpyDeviceConfig Clone() => new()
    {
        VideoCodec = VideoCodec,
        VideoBitRate = VideoBitRate,
        CameraFacing = CameraFacing,
        CameraSize = CameraSize,
        CameraFps = CameraFps,
        CameraHighSpeed = CameraHighSpeed,
        V4l2BufferMs = V4l2BufferMs,
        NoAudio = NoAudio,
        ExtraArguments = ExtraArguments
    };

    public void ApplyTo(ScrcpyOptions options)
    {
        options.VideoCodec = VideoCodec;
        options.VideoBitRate = string.IsNullOrWhiteSpace(VideoBitRate) ? null : VideoBitRate;
        options.CameraFacing = CameraFacing;
        options.CameraSize = string.IsNullOrWhiteSpace(CameraSize) ? null : CameraSize;
        options.CameraFps = CameraFps > 0 ? CameraFps : null;
        options.CameraHighSpeed = CameraHighSpeed;
        options.V4l2Buffer = Math.Max(0, V4l2BufferMs);
        options.NoAudio = NoAudio;

        foreach (var line in ExtraArguments.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            options.ExtraArguments.Add(line);
    }
}
