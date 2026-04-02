using System;
using System.Numerics;
using Silk.NET.Input;

namespace VoxEngine.Utils;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; set; }
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    private float _speed = 15.0f;
    private float _sensitivity = 0.1f;

    public float Yaw { get; set; }
    public float Pitch { get; set; }

    public Camera(Vector3 position, Vector3 target)
    {
        Position = position;
        var direction = Vector3.Normalize(target - position);
        Pitch = MathF.Asin(direction.Y) * (180f / MathF.PI);
        Yaw = MathF.Atan2(direction.Z, direction.X) * (180f / MathF.PI);
        UpdateCameraVectors();
    }

    public void OnMouseMove(Vector2 offset)
    {
        Yaw += offset.X * _sensitivity;
        Pitch -= offset.Y * _sensitivity;

        Pitch = Math.Clamp(Pitch, -89f, 89f);
        UpdateCameraVectors();
    }

    private void UpdateCameraVectors()
    {
        Vector3 front;
        front.X = MathF.Cos(Yaw * MathF.PI / 180f) * MathF.Cos(Pitch * MathF.PI / 180f);
        front.Y = MathF.Sin(Pitch * MathF.PI / 180f);
        front.Z = MathF.Sin(Yaw * MathF.PI / 180f) * MathF.Cos(Pitch * MathF.PI / 180f);
        Front = Vector3.Normalize(front);
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
