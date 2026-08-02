using System.ComponentModel;
using System.Diagnostics;

namespace ScrcpyCameraGui;

public static class V4l2LoopbackCtl
{
    public readonly record struct Result(bool Success, string? Error);

    public static Result ResetCaps(string devicePath) => Run("set-caps", devicePath, "any");

    public static bool IsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("v4l2loopback-ctl", "-h")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            proc.WaitForExit(2000);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Result Run(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("v4l2loopback-ctl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi);
            if (proc == null) return new Result(false, "Could not launch v4l2loopback-ctl.");

            proc.WaitForExit(3000);
            if (proc.ExitCode == 0) return new Result(true, null);

            var stderr = proc.StandardError.ReadToEnd();
            return new Result(false, string.IsNullOrWhiteSpace(stderr)
                ? $"v4l2loopback-ctl exited with code {proc.ExitCode}."
                : stderr.Trim());
        }
        catch (Win32Exception)
        {
            return new Result(false,
                "v4l2loopback-ctl was not found. Install it with: sudo dnf install v4l2loopback-utils");
        }
        catch (Exception ex)
        {
            return new Result(false, ex.Message);
        }
    }
}