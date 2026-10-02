using System.Diagnostics;
using System.Linq;

namespace ScrcpyCameraGui;

// ReSharper disable once InconsistentNaming
public static class V4l2ModuleManager
{
    private const int DefaultVideoNrBase = 64;

    public static bool IsLoaded => V4l2LoopbackInfo.IsModuleLoaded;
    public static int LoadedDeviceCount => V4l2LoopbackInfo.DiscoverDeviceIndexes().Count;

    public readonly record struct Result(bool Success, string? Error);

    public static Result Apply(int deviceCount, bool persist, int videoNrBase = DefaultVideoNrBase)
    {
        if (deviceCount < 1) deviceCount = 1;

        var optionsLine = BuildOptionsLine(deviceCount, videoNrBase);

        var lines = new List<string>();

        if (persist)
        {
            lines.Add("mkdir -p /etc/modprobe.d /etc/modules-load.d");
            lines.Add($"printf 'options v4l2loopback {optionsLine}\\n' > /etc/modprobe.d/v4l2loopback.conf");
            lines.Add("printf 'v4l2loopback\\n' > /etc/modules-load.d/v4l2loopback.conf");
        }

        lines.Add("modprobe -r v4l2loopback 2>/dev/null || true");
        lines.Add($"modprobe v4l2loopback {optionsLine}");

        return RunPrivileged(string.Join("\n", lines));
    }

    // exclusive_caps, video_nr and card_label are all per-device list parameters for some fucking reason
    private static string BuildOptionsLine(int deviceCount, int videoNrBase)
    {
        var videoNrs = Enumerable.Range(videoNrBase, deviceCount);
        var cardLabels = Enumerable.Range(1, deviceCount).Select(i => $"ScrcpyCamera {i}");
        var exclusiveCaps = Enumerable.Repeat("1", deviceCount);

        var videoNrArg = string.Join(",", videoNrs);
        var cardLabelArg = string.Join(",", cardLabels.Select(l => $"\"{l}\""));
        var exclusiveCapsArg = string.Join(",", exclusiveCaps);

        return $"devices={deviceCount} video_nr={videoNrArg} card_label={cardLabelArg} exclusive_caps={exclusiveCapsArg}";
    }

    private static Result RunPrivileged(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"scrcpy-gui-v4l2-{Guid.NewGuid():N}.sh");

        try
        {
            File.WriteAllText(scriptPath, "#!/bin/sh\nset -e\n" + script + "\n");

            var psi = new ProcessStartInfo("pkexec", $"sh {scriptPath}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc == null) return new Result(false, "Could not launch pkexec.");

            proc.WaitForExit(30000);

            if (proc.ExitCode == 0) return new Result(true, null);

            var stderr = proc.StandardError.ReadToEnd();
            return new Result(false, string.IsNullOrWhiteSpace(stderr)
                ? $"pkexec exited with code {proc.ExitCode}. (User may have cancelled the auth prompt.)"
                : stderr.Trim());
        }
        catch (Exception ex)
        {
            return new Result(false, ex.Message);
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* best effort */ }
        }
    }
}