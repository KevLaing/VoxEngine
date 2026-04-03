using System;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace VoxEngine;

public sealed class InputController : IDisposable
{
    private readonly IWindow _window;
    private readonly Action _toggleAlteredState;

    private IInputContext? _input;
    private Vector2 _lastMousePosition;
    private Vector2 _accumulatedLookDelta;
    private bool _jumpPressed;

    public InputController(IWindow window, Action toggleAlteredState)
    {
        _window = window;
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

    public PlayerInput BuildPlayerInput()
    {
        if (_input is null || _input.Keyboards.Count == 0)
            return default;

        IKeyboard keyboard = _input.Keyboards[0];
        float moveForward = 0f;
        float moveRight = 0f;

        if (keyboard.IsKeyPressed(Key.W))
            moveForward += 1f;
        if (keyboard.IsKeyPressed(Key.S))
            moveForward -= 1f;
        if (keyboard.IsKeyPressed(Key.D))
            moveRight += 1f;
        if (keyboard.IsKeyPressed(Key.A))
            moveRight -= 1f;

        PlayerInput input = new()
        {
            MoveForward = moveForward,
            MoveRight = moveRight,
            JumpPressed = _jumpPressed,
            SprintHeld = keyboard.IsKeyPressed(Key.ShiftLeft),
            LookDelta = _accumulatedLookDelta,
        };

        _jumpPressed = false;
        _accumulatedLookDelta = Vector2.Zero;
        return input;
    }

    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        Vector2 currentPosition = new(position.X, position.Y);
        Vector2 delta = currentPosition - _lastMousePosition;
        _lastMousePosition = currentPosition;
        _accumulatedLookDelta += delta;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int code)
    {
        if (key == Key.U)
            _toggleAlteredState();
        else if (key == Key.Space)
            _jumpPressed = true;
    }

    public void Dispose()
    {
        _input?.Dispose();
    }
}
