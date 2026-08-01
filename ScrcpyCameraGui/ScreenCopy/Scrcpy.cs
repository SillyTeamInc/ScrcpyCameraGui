using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScrcpyCameraGui.ScreenCopy;

public static class Scrcpy
{
    public static string? FindScrcpy()
    {
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        List<string> paths = pathEnv.Split(Path.PathSeparator).ToList();
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scrcpy"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "scrcpy"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "scrcpy"));
        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "scrcpy.exe" : "scrcpy";

        foreach (string path in paths)
        {
            string fullPath = Path.Combine(path, exeName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        string localPath = Path.Combine(AppContext.BaseDirectory, exeName);
        if (File.Exists(localPath)) return localPath;

        return null;
    }

    public static async Task<string> EnsureScrcpyAvailableAsync()
    {
        string? path = FindScrcpy();
        if (path != null) return path;

        return await DownloadScrcpyAsync();
    }

    private static async Task<string> DownloadScrcpyAsync()
    {
        throw new InvalidOperationException("scrcpy not found and auto-download is not fully implemented.");
    }

    public static Process? LaunchScrcpy(ScrcpyOptions options)
    {
        string? scrcpyPath = FindScrcpy();
        if (scrcpyPath == null)
        {
            AlertDialogManager.Show("scrcpy", "scrcpy was not found in your PATH. Please install scrcpy and ensure it is available in your system PATH.", AlertLevel.Error);
            return null;
        }

        var startInfo = new ProcessStartInfo(scrcpyPath);
        foreach (var arg in options.ToArguments())
        {
            startInfo.ArgumentList.Add(arg);
        }
        return Process.Start(startInfo)!;
    }
}
