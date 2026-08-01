using ScrcpyCameraGui.Device;

namespace ScrcpyCameraGui;

public sealed class DeviceRowCallbacks
{
    public required Action<DeviceSession> EnableWireless { get; init; }
    public required Func<DeviceSession, bool> IsPairing { get; init; }
    public required Action<DeviceSession> Disconnect { get; init; }
}
