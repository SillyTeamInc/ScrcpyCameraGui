using System.Numerics;
using ImGuiNET;
using ScrcpyCameraGui.ScreenCopy;
using SharpAdbClient;

namespace ScrcpyCameraGui.Render;

public interface IWidget
{
    void Render();
}

public sealed class DeviceRowWidget : IWidget
{
    private static readonly string[] CodecNames = { "H264", "H265", "AV1" };
    private static readonly string[] FacingNames = { "Front", "Back", "External" };

    private readonly DeviceSession _session;
    private readonly DeviceRowCallbacks _callbacks;
    private readonly ScrcpyConfigStore _configStore;

    private ScrcpyDeviceConfig? _editBuffer;

    public DeviceRowWidget(DeviceSession session, DeviceRowCallbacks callbacks, ScrcpyConfigStore configStore)
    {
        _session = session;
        _callbacks = callbacks;
        _configStore = configStore;
    }

    public void Render()
    {
        var dev = _session.Device;
        ImGui.PushID(dev.Serial);
        ImGui.BeginGroup();

        RenderSettingsPopup(dev.Serial);

        if (_session.State == SessionState.Reconnecting)
        {
            ImGui.Text($"{dev.Name}  ({dev.Serial})  [reconnecting]");
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f),
                _session.WasStreamingBeforeDisconnect
                    ? "Connection lost - will resume streaming automatically."
                    : "Connection lost - reconnecting...");

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

        ImGui.SameLine();
        if (ImGui.Button("Settings"))
        {
            _editBuffer = _configStore.Get(dev.Serial);
            ImGui.OpenPopup("Scrcpy Settings");
        }

        ImGui.EndGroup();
        ImGui.PopID();
        ImGui.Separator();
    }

    private void RenderSettingsPopup(string serial)
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.Appearing);

        var open = true;
        if (!ImGui.BeginPopupModal("Scrcpy Settings", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        if (_editBuffer == null)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        var cfg = _editBuffer;

        ImGui.TextDisabled("Applied on next launch");
        ImGui.Separator();
        ImGui.Spacing();

        var codecIndex = (int)cfg.VideoCodec;
        if (ImGui.Combo("Video codec", ref codecIndex, CodecNames, CodecNames.Length))
            cfg.VideoCodec = (VideoCodec)codecIndex;

        var bitRate = cfg.VideoBitRate;
        if (ImGui.InputText("Video bit rate", ref bitRate, 16))
            cfg.VideoBitRate = bitRate;

        var facingIndex = (int)cfg.CameraFacing;
        if (ImGui.Combo("Camera facing", ref facingIndex, FacingNames, FacingNames.Length))
            cfg.CameraFacing = (CameraFacing)facingIndex;

        var size = cfg.CameraSize;
        if (ImGui.InputText("Camera size (WxH)", ref size, 16))
            cfg.CameraSize = size;

        var fps = cfg.CameraFps;
        if (ImGui.InputInt("Camera FPS", ref fps))
            cfg.CameraFps = Math.Clamp(fps, 1, 240);

        var highSpeed = cfg.CameraHighSpeed;
        if (ImGui.Checkbox("High-speed capture", ref highSpeed))
            cfg.CameraHighSpeed = highSpeed;

        var v4l2Buffer = cfg.V4l2BufferMs;
        if (ImGui.InputInt("V4l2 buffer (ms)", ref v4l2Buffer))
            cfg.V4l2BufferMs = Math.Max(0, v4l2Buffer);
        ImGui.TextDisabled("0 = lowest latency (default)");

        var noAudio = cfg.NoAudio;
        if (ImGui.Checkbox("No audio", ref noAudio))
            cfg.NoAudio = noAudio;
        
        var extra = cfg.ExtraArguments;
        if (ImGui.InputTextMultiline("Extra arguments (one per line)", ref extra, 512, new Vector2(320, 60)))
            cfg.ExtraArguments = extra;

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button("Save", new Vector2(100, 0)))
        {
            _configStore.Save(serial, cfg);
            _editBuffer = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset to defaults", new Vector2(140, 0)))
        {
            _editBuffer = ScrcpyDeviceConfig.Default();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100, 0)))
        {
            _editBuffer = null;
            ImGui.CloseCurrentPopup();
        }

        if (!open)
        {
            _editBuffer = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }
}