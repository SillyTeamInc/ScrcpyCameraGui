using System.Numerics;
using ImGuiNET;

namespace ScrcpyCameraGui;

public enum AlertLevel
{
    Info,
    Warning,
    Error
}

public sealed class AlertMessage
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public AlertLevel Level { get; init; } = AlertLevel.Info;
}

public static class AlertDialogManager
{
    private static readonly Queue<AlertMessage> _queue = new();
    private static AlertMessage? _current;
    private static bool _open;

    public static void Show(string title, string body, AlertLevel level = AlertLevel.Info)
    {
        _queue.Enqueue(new AlertMessage { Title = title, Body = body, Level = level });
    }

    public static void Render()
    {
        if (_current == null && _queue.Count > 0)
        {
            _current = _queue.Dequeue();
            _open = true;
            ImGui.OpenPopup(PopupId(_current));
        }

        if (_current == null) return;

        var id = PopupId(_current);
        ImGui.SetNextWindowSize(new Vector2(400, 200));
        if (ImGui.BeginPopupModal(id, ref _open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var color = _current.Level switch
            {
                AlertLevel.Error => new Vector4(0.9f, 0.3f, 0.3f, 1f),
                AlertLevel.Warning => new Vector4(0.9f, 0.7f, 0.2f, 1f),
                _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
            };
            ImGui.TextColored(color, _current.Level.ToString());
            ImGui.Separator();
            ImGui.TextWrapped(_current.Body);
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();

            if (ImGui.Button("OK", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
                _current = null;
            }

            ImGui.EndPopup();
        }
        else if (!_open)
        {
            _current = null;
        }
    }

    private static string PopupId(AlertMessage msg) => $"{msg.Title}##alert";
}
