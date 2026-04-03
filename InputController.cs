using System;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;
using VoxEngine.Utils;

namespace VoxEngine;

public sealed class InputController : IDisposable
{
    private readonly IWindow _window;
    private readonly Camera _camera;
    private readonly Action _toggleAlteredState;

    private IInputContext? _input;
    private Vector2 _lastMousePosition;

    public InputController(IWindow window, Camera camera, Action toggleAlteredState)
    {
        _window = window;
        _camera = camera;
        _toggleAlteredState = toggleAlteredState;
    }

    public void Initialize()
    {
        _input = _window.CreateInput();

        if (_input.Mice.Count > 0)
        {
            IMouse mouse = _input.Mice[0];
            mouse.Cursor.CursorMode = CursorMode.Disabled;
            _lastMousePosition = new Vector2(mouse.Position.X, mouse.Position.Y);
            mouse.MouseMove += OnMouseMove;
        }

        if (_input.Keyboards.Count > 0)
        {
            _input.Keyboards[0].KeyDown += OnKeyDown;
        }
    }

    public void Update(double delta)
    {
        if (_input is null || _input.Keyboards.Count == 0)
            return;

        _camera.OnUpdate(delta, _input.Keyboards[0]);
    }

    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        Vector2 currentPosition = new(position.X, position.Y);
        Vector2 delta = currentPosition - _lastMousePosition;
        _lastMousePosition = currentPosition;
        _camera.OnMouseMove(delta);
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int code)
    {
        if (key == Key.U)
            _toggleAlteredState();
    }

    public void Dispose()
    {
        _input?.Dispose();
    }
}
