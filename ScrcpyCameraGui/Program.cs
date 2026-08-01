using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace ScrcpyCameraGui;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;
    private static ImGuiController _imGuiController = null!;
    private static readonly Stopwatch _frameSw = new();
    private static MainWindow _mainWindow = null!;

    static void Main()
    {
        try
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(800, 600);
            options.Title = "scrcpy camera gui";
            options.VSync = true;

            _window = Window.Create(options);

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                _mainWindow?.Shutdown();
            };

            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Closing += OnClose;

            _window.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private static void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        var inputContext = _window.CreateInput();

        ResourceUtil.LoadResourceBytes();

        _imGuiController = new ImGuiController(
            _gl,
            _window,
            inputContext,
            onConfigureIO: () =>
            {
                var io = ImGuiNET.ImGui.GetIO();
                io.Fonts.AddFontDefault();
                ResourceUtil.RegisterFontsWithImGui();
            }
        );

        _mainWindow = new MainWindow();
    }

    private static unsafe void OnRender(double deltaSeconds)
    {
        _frameSw.Restart();

        _gl.ClearColor(41f / 255, 44f / 255, 48f / 255, 1.0f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        _imGuiController.Update((float)deltaSeconds);

        var font = ResourceUtil.GetFont("product_sans.ttf");
        var pushedFont = font.NativePtr != null;
        if (pushedFont) ImGuiNET.ImGui.PushFont(font);

        _mainWindow.Render();

        if (pushedFont) ImGuiNET.ImGui.PopFont();

        _imGuiController.Render();

        _frameSw.Stop();
        if (_frameSw.Elapsed.TotalMilliseconds > 8)
        {
            Console.WriteLine($"Slow frame: {_frameSw.Elapsed.TotalMilliseconds:0.00}ms");
        }
    }

    private static void OnFramebufferResize(Vector2D<int> newSize)
    {
        _gl.Viewport(newSize);
    }

    private static void OnClose()
    {
        _mainWindow?.Shutdown();
        _imGuiController?.Dispose();
        _gl?.Dispose();
    }
}
