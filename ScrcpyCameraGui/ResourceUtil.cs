using System.Reflection;
using ImGuiNET;

namespace ScrcpyCameraGui;

public struct Resource
{
    public string Key;
    public string Path;
    public byte[] Data;
}

public static class ResourceUtil
{
    private const string ResourcePrefix = "ScrcpyCameraGui.Resources.";
    private const float DefaultFontSize = 20f;

    private static readonly List<Resource> Resources = new();
    private static readonly Dictionary<string, ImFontPtr> Fonts = new();
    private static readonly Lock _resorceLock = new();

    public static void LoadResourceBytes()
    {
        lock (_resorceLock)
        {
            Resources.Clear();
            Fonts.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                using Stream? stream = assembly.GetManifestResourceStream(resource);
                if (stream == null)
                {
                    continue;
                }

                string path = ConvertPath(resource);

                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                byte[] data = memoryStream.ToArray();

                Console.WriteLine($"Loaded resource bytes {path} ({data.Length} bytes)");
                Resources.Add(new Resource { Key = path, Path = path, Data = data });
            }
        }
    }

    public static unsafe void RegisterFontsWithImGui(float sizePixels = DefaultFontSize)
    {
        lock (_resorceLock)
        {
            var io = ImGui.GetIO();

            foreach (var res in Resources)
            {
                if (!res.Path.EndsWith(".ttf"))
                {
                    continue;
                }

                fixed (byte* fontPtr = res.Data)
                {
                    ImFontPtr font = io.Fonts.AddFontFromMemoryTTF(
                        (IntPtr)fontPtr, res.Data.Length, sizePixels);
                    Fonts[res.Path] = font;
                    Console.WriteLine($"Registered font {res.Path} ({res.Data.Length} bytes)");
                }
            }
        }
    }

    public static Resource GetResource(string key)
    {
        lock (_resorceLock)
        {
            return Resources.First(r => r.Key == key);
        }
    }

    public static ImFontPtr GetFont(string key)
    {
        lock (_resorceLock)
        {
            if (!Fonts.TryGetValue(key, out ImFontPtr font))
            {
                throw new KeyNotFoundException($"No font loaded for key '{key}'");
            }

            return font;
        }
    }

    private static string ConvertPath(string path)
    {
        string trimmed = path.StartsWith(ResourcePrefix)
            ? path[ResourcePrefix.Length..]
            : path;

        int lastDot = trimmed.LastIndexOf('.');
        if (lastDot < 0)
        {
            return trimmed.Replace('.', '/');
        }

        string namePart = trimmed[..lastDot];
        string ext = trimmed[lastDot..];

        return namePart.Replace('.', '/') + ext;
    }
}