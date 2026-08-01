namespace ScrcpyCameraGui;

public sealed class V4l2SinkPool
{
    private const string SysfsVideo4LinuxRoot = "/sys/class/video4linux";
    private readonly HashSet<int> _inUse = new();

    public string Acquire()
    {
        var available = DiscoverLoopbackDevices();

        if (!TryFindFree(available, out var free))
        {
            var loaded = available.Count == 0
                ? "none found"
                : string.Join(", ", available.ConvertAll(i => $"/dev/video{i}"));

            throw new InvalidOperationException(
                $"No free v4l2loopback device available (loaded: {loaded}). \n" +
                "Load or reload the module with more devices.\n" +
                "sudo modprobe v4l2loopback devices=2 exclusive_caps=1");
        }

        _inUse.Add(free);
        return $"/dev/video{free}";
    }

    private bool TryFindFree(List<int> available, out int free)
    {
        foreach (var index in available)
        {
            if (_inUse.Contains(index)) continue;
            free = index;
            return true;
        }

        free = -1;
        return false;
    }

    public void Release(string sinkPath)
    {
        var numberPart = sinkPath.Replace("/dev/video", "");
        if (int.TryParse(numberPart, out var index))
            _inUse.Remove(index);
    }

    private static List<int> DiscoverLoopbackDevices()
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
