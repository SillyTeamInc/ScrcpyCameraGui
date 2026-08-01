using System.Text.RegularExpressions;

namespace ScrcpyCameraGui;

public static class WirelessDevice
{
    private static readonly Regex SerialPattern =
        new(@"^(?<host>[^:\s]+):(?<port>\d{1,5})$", RegexOptions.Compiled);

    public static bool IsWireless(string serial) => SerialPattern.IsMatch(serial);

    public static bool TryParseEndpoint(string serial, out string host, out int port)
    {
        var match = SerialPattern.Match(serial);
        if (match.Success)
        {
            host = match.Groups["host"].Value;
            port = int.Parse(match.Groups["port"].Value);
            return true;
        }

        host = "";
        port = 0;
        return false;
    }
}
