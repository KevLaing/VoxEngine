using System.Numerics;
using Silk.NET.Input;

namespace VoxEngine.Utils;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; set; }
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    private float _speed = 15.0f;

    public Camera(Vector3 position, Vector3 target)
    {
        Position = position;
        Front = Vector3.Normalize(target - position);
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, Up);
    }

    public void OnUpdate(double deltaTime, IKeyboard keyboard)
    {
        float moveSpeed = _speed * (float)deltaTime;
        
        // Calculate Right vector for strafing
        var right = Vector3.Normalize(Vector3.Cross(Front, Up));

        // Forward/Back
        if (keyboard.IsKeyPressed(Key.W)) Position += Front * moveSpeed;
        if (keyboard.IsKeyPressed(Key.S)) Position -= Front * moveSpeed;

        // Strafe
        if (keyboard.IsKeyPressed(Key.A)) Position -= right * moveSpeed;
        if (keyboard.IsKeyPressed(Key.D)) Position += right * moveSpeed;

        // Elevation
        if (keyboard.IsKeyPressed(Key.E)) Position += Up * moveSpeed;
        if (keyboard.IsKeyPressed(Key.ShiftLeft)) Position -= Up * moveSpeed;
    }
}
