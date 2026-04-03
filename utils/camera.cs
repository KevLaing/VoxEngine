using System.Numerics;

namespace VoxEngine.Utils;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; private set; }
    public Vector3 Up { get; private set; } = Vector3.UnitY;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    public Camera(Vector3 position, Vector3 target)
    {
        Position = position;
        var direction = Vector3.Normalize(target - position);
        Pitch = MathF.Asin(direction.Y);
        Yaw = MathF.Atan2(direction.Z, direction.X);
        UpdateCameraVectors();
    }

    public void SetOrientation(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = pitch;
        UpdateCameraVectors();
    }

    private void UpdateCameraVectors()
    {
        Vector3 front;
        front.X = MathF.Cos(Yaw) * MathF.Cos(Pitch);
        front.Y = MathF.Sin(Pitch);
        front.Z = MathF.Sin(Yaw) * MathF.Cos(Pitch);
        Front = Vector3.Normalize(front);
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, Up);
    }
}
