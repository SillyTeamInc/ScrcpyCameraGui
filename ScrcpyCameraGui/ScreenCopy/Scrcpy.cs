using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ScrcpyCameraGui.ScreenCopy;

public static class Scrcpy
{
    // Hardcoded because i'm lazy
    private const string ReleaseVersion = "v4.1";
    
    
    public static string? FindScrcpy()
    {
        Console.WriteLine("Searching for scrcpy...");

        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        List<string> paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scrcpy"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "scrcpy"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "scrcpy"));

        foreach (string path in paths)
        {
            string fullPath = Path.Combine(path, "scrcpy");
            Console.WriteLine($"Checking {fullPath}");
            if (File.Exists(fullPath))
            {
                Console.WriteLine($"Found scrcpy at {fullPath}");
                return fullPath;
            }
        }

        foreach (string path in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scrcpy"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "scrcpy"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "scrcpy"),
                     AppContext.BaseDirectory
                 })
        {
            string? fullPath = FindRecursively(path, "scrcpy");
            if (fullPath != null)
            {
                Console.WriteLine($"Found scrcpy recursively at {fullPath}");
                return fullPath;
            }
        }

        Console.WriteLine("scrcpy was not found locally.");
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
        string installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "scrcpy");
        Directory.CreateDirectory(installRoot);

        Console.WriteLine($"Downloading scrcpy {ReleaseVersion} into {installRoot}");

        using HttpClient httpClient = new();
        foreach (string assetName in GetAssetCandidates())
        {
            Console.WriteLine($"Trying asset {assetName}");
            string? path = await TryDownloadAndExtractAsync(httpClient, installRoot, assetName, "scrcpy")
                .ConfigureAwait(false);
            if (path != null)
            {
                Console.WriteLine($"Downloaded scrcpy to {path}");
                return path;
            }
        }

        throw new InvalidOperationException(
            $"Unable to download scrcpy {ReleaseVersion} for Linux {RuntimeInformation.ProcessArchitecture}.");
    }

    private static IEnumerable<string> GetAssetCandidates()
    {
        return RuntimeInformation.ProcessArchitecture switch
        { 
            Architecture.X64 => new[] { $"scrcpy-linux-x86_64-{ReleaseVersion}.tar.gz" },
            Architecture.Arm64 => new[] { $"scrcpy-linux-aarch64-{ReleaseVersion}.tar.gz", $"scrcpy-linux-arm64-{ReleaseVersion}.tar.gz" },
            _ => throw new PlatformNotSupportedException($"Unsupported Linux architecture: {RuntimeInformation.ProcessArchitecture}")
        };
    }

    private static async Task<string?> TryDownloadAndExtractAsync(HttpClient httpClient, string installRoot,
        string assetName, string exeName)
    {
        string url = $"https://github.com/Genymobile/scrcpy/releases/download/{ReleaseVersion}/{assetName}";
        Console.WriteLine($"Downloading {url}");
        using HttpResponseMessage response =
            await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Asset {assetName} was not available ({(int)response.StatusCode} {response.ReasonPhrase})");
            return null;
        }

        string tempArchivePath = Path.GetTempFileName();
        string extractRoot = Path.Combine(installRoot, RemoveArchiveExtensions(assetName));

        try
        {
            await using (Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (FileStream archiveStream =
                         File.Open(tempArchivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await responseStream.CopyToAsync(archiveStream).ConfigureAwait(false);
            }

            if (Directory.Exists(extractRoot))
            {
                Directory.Delete(extractRoot, true);
            }

            Directory.CreateDirectory(extractRoot);
            Console.WriteLine($"Extracting {assetName} to {extractRoot}");

            if (!assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Unsupported scrcpy asset format: {assetName}");
            }

            string tempTarPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".tar");
            try
            {
                await using (FileStream gzStream = File.OpenRead(tempArchivePath))
                await using (GZipStream decompressed = new(gzStream, CompressionMode.Decompress))
                await using (FileStream tarStream =
                             File.Open(tempTarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await decompressed.CopyToAsync(tarStream).ConfigureAwait(false);
                }

                await TarFile.ExtractToDirectoryAsync(tempTarPath, extractRoot, overwriteFiles: true);
            }
            finally
            {
                if (File.Exists(tempTarPath))
                {
                    File.Delete(tempTarPath);
                }
            }

            string? extractedPath = FindRecursively(extractRoot, exeName);
            if (extractedPath == null)
            {
                Console.WriteLine($"Could not find {exeName} after extracting {assetName}");
                return null;
            }

            EnsureExecutableBit(extractedPath);
            Console.WriteLine($"Prepared executable {extractedPath}");
            return extractedPath;
        }
        finally
        {
            if (File.Exists(tempArchivePath))
            {
                File.Delete(tempArchivePath);
            }
        }
    }

    private static string RemoveArchiveExtensions(string assetName)
    {
        if (assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return assetName[..^7];
        }

        return Path.GetFileNameWithoutExtension(assetName);
    }

    private static string? FindRecursively(string directory, string fileName)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void EnsureExecutableBit(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(
            path,
            mode |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherExecute);
    }

    public static Process? LaunchScrcpy(ScrcpyOptions options)
    {
        string? scrcpyPath = FindScrcpy();
        if (scrcpyPath == null)
        {
            Console.WriteLine("Launch failed because scrcpy could not be found.");
            AlertDialogManager.Show("scrcpy",
                "scrcpy was not found in your PATH. Please install scrcpy and ensure it is available in your system PATH.",
                AlertLevel.Error);
            return null;
        }

        Console.WriteLine($"Launching scrcpy from {scrcpyPath}");
        var startInfo = new ProcessStartInfo(scrcpyPath);
        foreach (var arg in options.ToArguments())
        {
            startInfo.ArgumentList.Add(arg);
        }

        return Process.Start(startInfo);
    }
}