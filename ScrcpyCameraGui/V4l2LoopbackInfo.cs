namespace ScrcpyCameraGui;

// ReSharper disable once InconsistentNaming
public static class V4l2LoopbackInfo
{
    private const string SysfsVideo4LinuxRoot = "/sys/class/video4linux";

    public static bool IsModuleLoaded => Directory.Exists("/sys/module/v4l2loopback");

    public static List<int> DiscoverDeviceIndexes()
    {
        var result = new List<int>();

        if (!Directory.Exists(SysfsVideo4LinuxRoot))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(SysfsVideo4LinuxRoot, "video*"))
        {
            var name = Path.GetFileName(dir);
            if (!name.StartsWith("video") || !int.TryParse(name.AsSpan(5), out var index))
                continue;

            var target = ResolveSymlinkTarget(dir);
            if (target != null && target.Contains("/virtual/video4linux/"))
                result.Add(index);
        }

        result.Sort();
        return result;
    }

    private static string? ResolveSymlinkTarget(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (info.LinkTarget == null) return Path.GetFullPath(path);

            var baseDir = Path.GetDirectoryName(path) ?? "/";
            return Path.GetFullPath(info.LinkTarget, baseDir);
        }
        catch
        {
            return null;
        }
    }
}
