namespace ScrcpyCameraGui;

public sealed class V4l2SinkPool
{
    private const string ObsVirtualCameraName = "OBS Virtual Camera";

    private readonly HashSet<int> _inUse = new();

    public string Acquire()
    {
        var available = V4l2LoopbackInfo.DiscoverDeviceIndexes()
            .Where(index => !IsObsVirtualCamera(index))
            .ToList();

        if (!TryFindFree(available, out var free))
        {
            var loaded = available.Count == 0
                ? "none found"
                : string.Join(", ", available.ConvertAll(i => $"/dev/video{i}"));

            throw new InvalidOperationException(
                $"No free v4l2loopback device available (loaded: {loaded}). " +
                "Use the \"V4l2 loopback module\" section below to load more devices.");
        }

        _inUse.Add(free);
        return $"/dev/video{free}";
    }

    public void Release(string sinkPath)
    {
        var numberPart = sinkPath.Replace("/dev/video", "");
        if (int.TryParse(numberPart, out var index))
            _inUse.Remove(index);
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

    private static bool IsObsVirtualCamera(int index)
    {
        var namePath = $"/sys/class/video4linux/video{index}/name";

        try
        {
            if (!File.Exists(namePath))
            {
                return false;
            }
            // stupid ass workaround
            var deviceName = File.ReadAllText(namePath).Trim();
            return deviceName.Contains(ObsVirtualCameraName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
