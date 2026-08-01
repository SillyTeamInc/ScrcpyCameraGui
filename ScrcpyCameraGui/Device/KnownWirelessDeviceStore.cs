using System.Linq;
using System.Text.Json;

namespace ScrcpyCameraGui;

public sealed record KnownWirelessDevice(string Serial, string Label);

public sealed class KnownWirelessDeviceStore
{
    private readonly string _path;
    private readonly Dictionary<string, KnownWirelessDevice> _bySerial = new();

    public KnownWirelessDeviceStore()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScrcpyCameraGui");
        Directory.CreateDirectory(configDir);
        _path = Path.Combine(configDir, "known-wireless-devices.json");

        Load();
    }

    public IReadOnlyCollection<KnownWirelessDevice> All => _bySerial.Values;

    public void Remember(string serial, string label)
    {
        if (_bySerial.TryGetValue(serial, out var existing) && existing.Label == label)
            return;

        _bySerial[serial] = new KnownWirelessDevice(serial, label);
        Save();
    }

    public void Forget(string serial)
    {
        if (_bySerial.Remove(serial))
            Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            Console.WriteLine("loading wireless device store from " + _path);
            var json = File.ReadAllText(_path);
            var entries = JsonSerializer.Deserialize<List<KnownWirelessDevice>>(json) ?? new();
            foreach (var entry in entries)
                _bySerial[entry.Serial] = entry;
        }
        catch
        {
            // Corrupt or unreadable file
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                _bySerial.Values.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best effort
        }
    }
}
