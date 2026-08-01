using System.Diagnostics;

namespace ScrcpyCameraGui;

// ReSharper disable once InconsistentNaming
public static class V4l2ModuleManager
{
    public static bool IsLoaded => V4l2LoopbackInfo.IsModuleLoaded;
    public static int LoadedDeviceCount => V4l2LoopbackInfo.DiscoverDeviceIndexes().Count;

    public readonly record struct Result(bool Success, string? Error);

    public static Result Apply(int deviceCount, bool persist)
    {
        if (deviceCount < 1) deviceCount = 1;

        var lines = new List<string>();

        if (persist)
        {
            lines.Add("mkdir -p /etc/modprobe.d /etc/modules-load.d");
            lines.Add($"printf 'options v4l2loopback devices=%d exclusive_caps=1\\n' {deviceCount} > /etc/modprobe.d/v4l2loopback.conf");
            lines.Add("printf 'v4l2loopback\\n' > /etc/modules-load.d/v4l2loopback.conf");
        }

        lines.Add("modprobe -r v4l2loopback 2>/dev/null || true");
        lines.Add($"modprobe v4l2loopback devices={deviceCount} exclusive_caps=1");

        return RunPrivileged(string.Join("\n", lines));
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
