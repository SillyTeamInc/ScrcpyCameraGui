using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using SharpAdbClient;

namespace ScrcpyCameraGui;

public sealed class AdbService
{
    private readonly AdbClient _client = new();
    private readonly TimeSpan _pollInterval;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(2);

    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private bool _serverStarted;

    private readonly HashSet<string> _knownWirelessSerials = new();
    private readonly Dictionary<string, DateTimeOffset> _lastReconnectAttempt = new();

    private readonly HashSet<string> _userDisabledSerials = new();

    public IReadOnlyList<DeviceData> Devices { get; private set; } = new List<DeviceData>();

    public event Action<DeviceData>? DeviceConnected;
    public event Action<DeviceData>? DeviceDisconnected;
    public event Action<string>? Error;

    public AdbService(TimeSpan? pollInterval = null)
    {
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    public void Tick()
    {
        if (DateTimeOffset.UtcNow - _lastRefresh < _pollInterval) return;
        _lastRefresh = DateTimeOffset.UtcNow;
        Refresh();
    }

    public void Refresh()
    {
        EnsureServerStarted();

        try
        {
            var updated = _client.GetDevices();

            foreach (var dev in updated)
            {
                if (WirelessDevice.IsWireless(dev.Serial))
                    _knownWirelessSerials.Add(dev.Serial);
            }

            DiffAndNotify(updated);
            Devices = updated;

            AttemptReconnects(updated);
        }
        catch (SocketException)
        {
            _serverStarted = false;
            Error?.Invoke("Lost connection to the adb server, will try to restart it.");
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Failed to list devices: {ex.Message}");
        }
    }

    private void DiffAndNotify(IReadOnlyList<DeviceData> updated)
    {
        var oldBySerial = Devices.ToDictionary(d => d.Serial);
        var newBySerial = updated.ToDictionary(d => d.Serial);

        foreach (var dev in updated)
        {
            var wasOnline = oldBySerial.TryGetValue(dev.Serial, out var old) && IsOnline(old);
            if (IsOnline(dev) && !wasOnline)
                DeviceConnected?.Invoke(dev);
        }

        foreach (var dev in Devices)
        {
            var isStillOnline = newBySerial.TryGetValue(dev.Serial, out var current) && IsOnline(current);
            if (IsOnline(dev) && !isStillOnline)
                DeviceDisconnected?.Invoke(dev);
        }
    }

    private static bool IsOnline(DeviceData dev) => dev.State == DeviceState.Online;

    private void AttemptReconnects(IReadOnlyList<DeviceData> current)
    {
        if (_knownWirelessSerials.Count == 0) return;

        var onlineSerials = new HashSet<string>(current.Where(IsOnline).Select(d => d.Serial));

        foreach (var serial in _knownWirelessSerials)
        {
            if (onlineSerials.Contains(serial)) continue;
            if (_userDisabledSerials.Contains(serial)) continue;

            if (_lastReconnectAttempt.TryGetValue(serial, out var last) &&
                DateTimeOffset.UtcNow - last < ReconnectInterval)
                continue;

            _lastReconnectAttempt[serial] = DateTimeOffset.UtcNow;

            if (!WirelessDevice.TryParseEndpoint(serial, out var host, out var port))
                continue;

            try
            {
                _client.Connect(new DnsEndPoint(host, port));
            }
            catch
            {
                // stinky   
            }
        }
    }

    public bool ConnectWireless(string serial)
    {
        if (!WirelessDevice.TryParseEndpoint(serial, out var host, out var port))
        {
            Error?.Invoke($"'{serial}' doesn't look like a host:port address.");
            return false;
        }

        _userDisabledSerials.Remove(serial);

        try
        {
            _client.Connect(new DnsEndPoint(host, port));
            _knownWirelessSerials.Add(serial);
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Couldn't connect to {serial}: {ex.Message}");
            return false;
        }
    }

    public bool Disconnect(string serial)
    {
        _userDisabledSerials.Add(serial);

        try
        {
            var psi = new ProcessStartInfo(ResolveAdbPath(), $"disconnect {serial}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            proc.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Failed to disconnect {serial}: {ex.Message}");
            return false;
        }
    }

    public string? EnableWireless(DeviceData usbDevice, int port = 5555)
    {
        var ip = GetWirelessIp(usbDevice);
        if (ip == null)
        {
            Error?.Invoke($"Could not determine a wifi IP address for {usbDevice.Name}. Make sure it's connected to wifi.");
            return null;
        }

        if (!EnableTcpIp(usbDevice, port))
            return null;

        var serial = $"{ip}:{port}";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Thread.Sleep(1000);
            try
            {
                _client.Connect(new DnsEndPoint(ip, port));
                _knownWirelessSerials.Add(serial);
                _userDisabledSerials.Remove(serial);
                return serial;
            }
            catch
            {
                // adbd probably hasn't restarted yet
            }
        }

        Error?.Invoke($"Switched {usbDevice.Name} to tcpip mode, but couldn't connect to {serial}.");
        return null;
    }

    public string? GetWirelessIp(DeviceData usbDevice)
    {
        (string Command, Func<string, string?> Parse)[] probes =
        {
            ("ip -f inet addr show wlan0", ParseIpAddrShow),
            ("ip route get 1.1.1.1", ParseIpRouteGet),
            ("netcfg", ParseNetcfg)
        };

        foreach (var (command, parse) in probes)
        {
            try
            {
                var receiver = new ConsoleOutputReceiver();
                _client.ExecuteRemoteCommand(command, usbDevice, receiver);
                var ip = parse(receiver.ToString());
                if (ip != null) return ip;
            }
            catch
            {
                // Not every device supports every probe command
            }
        }

        return null;
    }

    private static string? ParseIpAddrShow(string output)
    {
        var match = Regex.Match(output, @"inet (\d{1,3}(?:\.\d{1,3}){3})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ParseIpRouteGet(string output)
    {
        var match = Regex.Match(output, @"src (\d{1,3}(?:\.\d{1,3}){3})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ParseNetcfg(string output)
    {
        var match = Regex.Match(output, @"wlan0\s+\S+\s+(\d{1,3}(?:\.\d{1,3}){3})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private bool EnableTcpIp(DeviceData usbDevice, int port)
    {
        try
        {
            var psi = new ProcessStartInfo(ResolveAdbPath(), $"-s {usbDevice.Serial} tcpip {port}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Error?.Invoke($"Could not launch adb to enable tcpip mode for {usbDevice.Name}.");
                return false;
            }

            proc.WaitForExit(5000);
            if (proc.ExitCode == 0) return true;

            var stderr = proc.StandardError.ReadToEnd();
            Error?.Invoke($"adb tcpip {port} failed for {usbDevice.Name}: {stderr}");
            return false;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Failed to enable tcpip mode for {usbDevice.Name}: {ex.Message}");
            return false;
        }
    }

    private void EnsureServerStarted()
    {
        if (_serverStarted) return;

        try
        {
            var adbPath = ResolveAdbPath();
            var server = new AdbServer();
            var result = server.StartServer(adbPath, restartServerIfNewer: false);

            _serverStarted = result is StartServerResult.Started or StartServerResult.AlreadyRunning;

            if (!_serverStarted)
                Error?.Invoke($"adb server did not start ({result}). Is adb installed and on PATH?");
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Could not start adb server: {ex.Message}");
        }
    }

    private static string ResolveAdbPath()
    {
        var adbPath = Environment.GetEnvironmentVariable("ADB_PATH");
        if (!string.IsNullOrWhiteSpace(adbPath))
            return adbPath;

        string[] fileNames = ["adb"];

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return "adb";

        foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var fileName in fileNames)
            {
                var maybe = Path.Combine(directory, fileName);
                if (File.Exists(maybe))
                {
                    Console.WriteLine("Found ADB at " + maybe);
                    return maybe;
                }
            }
        }

        return "adb";
    }

    public void KillServer()
    {
        try
        {
            _client.KillAdb();
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Failed to kill adb server: {ex.Message}");
        }
        finally
        {
            _serverStarted = false;
        }
    }
}
