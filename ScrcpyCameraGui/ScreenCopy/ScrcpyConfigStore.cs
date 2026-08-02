using System.Text.Json;

namespace ScrcpyCameraGui.ScreenCopy;

public sealed class ScrcpyConfigStore
{
    private readonly string _path;
    private readonly Dictionary<string, ScrcpyDeviceConfig> _bySerial = new();

    public ScrcpyConfigStore()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScrcpyCameraGui");
        Directory.CreateDirectory(configDir);
        _path = Path.Combine(configDir, "scrcpy-device-configs.json");

        Load();
    }

    public ScrcpyDeviceConfig Get(string serial) =>
        _bySerial.TryGetValue(serial, out var config) ? config.Clone() : ScrcpyDeviceConfig.Default();

    public void Save(string serial, ScrcpyDeviceConfig config)
    {
        _bySerial[serial] = config.Clone();
        Persist();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var json = File.ReadAllText(_path);
            var entries = JsonSerializer.Deserialize<Dictionary<string, ScrcpyDeviceConfig>>(json) ?? new();
            foreach (var (serial, config) in entries)
                _bySerial[serial] = config;
        }
        catch
        {
            // Corrupt or unreadable file
        }
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(_bySerial, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best effort
        }
    }
}
