using System.Numerics;
using ImGuiNET;
using ScrcpyCameraGui.Device;
using SharpAdbClient;

namespace ScrcpyCameraGui.Render;

public interface IWidget
{
    void Render();
}

public sealed class DeviceRowWidget : IWidget
{
    private readonly DeviceSession _session;
    private readonly DeviceRowCallbacks _callbacks;

    public DeviceRowWidget(DeviceSession session, DeviceRowCallbacks callbacks)
    {
        _session = session;
        _callbacks = callbacks;
    }

    public void Render()
    {
        var dev = _session.Device;
        ImGui.PushID(dev.Serial);
        ImGui.BeginGroup();
        
        if (_session.State == SessionState.Reconnecting)
        {
            ImGui.Text($"{dev.Name}  ({dev.Serial})  [reconnecting]");
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f),
                _session.WasStreamingBeforeDisconnect
                    ? "Connection lost, will resume streaming automatically."
                    : "Connection lost, attempting to reconnect...");

            if (_session.LastError != null)
                ImGui.TextDisabled($"Last attempt: {_session.LastError}");

            if (ImGui.Button("Stop trying"))
                _callbacks.Disconnect(_session);

            ImGui.EndGroup();
            ImGui.PopID();
            ImGui.Separator();
            return;
        }

        ImGui.Text($"{dev.Name}  ({dev.Serial})  [{dev.State}]");

        if (dev.State != DeviceState.Online)
        {
            ImGui.TextDisabled("Device not ready.");
            ImGui.EndGroup();
            ImGui.PopID();
            ImGui.Separator();
            return;
        }

        switch (_session.State)
        {
            case SessionState.Stopped:
            case SessionState.Failed:
                if (ImGui.Button("Launch scrcpy"))
                    _session.Start();
                break;

            case SessionState.Starting:
                ImGui.TextDisabled("Starting...");
                break;

            case SessionState.Running:
                if (ImGui.Button("Stop scrcpy"))
                    _session.Stop();
                ImGui.SameLine();
                ImGui.TextDisabled($"-> {_session.SinkPath}");
                break;
        }

        if (_session.State == SessionState.Failed && _session.LastError != null)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.3f, 0.3f, 1f), $"Error: {_session.LastError}");
        }

        if (_session.IsWireless)
        {
            ImGui.SameLine();
            if (ImGui.Button("Disconnect"))
                _callbacks.Disconnect(_session);
        }
        else if (_callbacks.IsPairing(_session))
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Enabling wireless...");
        }
        else
        {
            ImGui.SameLine();
            if (ImGui.Button("Enable Wireless"))
                _callbacks.EnableWireless(_session);
        }

        ImGui.EndGroup();
        ImGui.PopID();
        ImGui.Separator();
    }
}
