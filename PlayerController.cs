using System;
using System.Numerics;
using VoxEngine.Utils;

namespace VoxEngine;

public sealed class PlayerController
{
    public const float ColliderRadius = 0.35f;
    public const float ColliderHeight = 1.80f;
    public const float EyeHeight = 1.65f;
    public const float MaxStepHeight = 10.0f;

    private const float WalkSpeed = 15.0f;
    private const float SprintSpeed = 27.0f;
    private const float GroundAcceleration = 30.0f;
    private const float AirAcceleration = 10.0f;
    private const float GroundFriction = 12.0f;
    private const float Gravity = 20.0f;
    private const float JumpHeight = 10.10f;
    private const float TerminalVelocity = 40.0f;
    private const float MouseSensitivity = 0.0025f;
    private const float MaxPitch = 1.55f;
    private const float CollisionStepSize = 0.05f;
    private const float GroundProbeDistance = 0.05f;
    private const float GroundSettleStepSize = 0.05f;

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public bool IsGrounded { get; private set; }

    private static readonly float JumpSpeed = MathF.Sqrt(2f * Gravity * JumpHeight);

    public PlayerController(Vector3 position, float yaw = 0f, float pitch = 0f)
    {
        Position = position;
        Yaw = yaw;
        Pitch = pitch;
    }

    public void Update(World world, PlayerInput input, float dt)
    {
        ApplyLook(input.LookDelta);

        Vector3 wishDirection = GetWishDirection(input.MoveForward, input.MoveRight);
        float targetSpeed = input.SprintHeld ? SprintSpeed : WalkSpeed;
        Vector2 horizontalVelocity = new(Velocity.X, Velocity.Z);
        Vector2 targetHorizontalVelocity = new(wishDirection.X * targetSpeed, wishDirection.Z * targetSpeed);

        if (IsGrounded)
        {
            if (targetHorizontalVelocity.LengthSquared() > 0f)
            {
                horizontalVelocity = Accelerate(horizontalVelocity, targetHorizontalVelocity, GroundAcceleration, dt);
            }
            else
            {
                horizontalVelocity = ApplyFriction(horizontalVelocity, GroundFriction, dt);
            }
        }
        else
        {
            horizontalVelocity = Accelerate(horizontalVelocity, targetHorizontalVelocity, AirAcceleration, dt);
        }

        Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Y);

        if (IsGrounded && input.JumpPressed)
        {
            Velocity = new Vector3(Velocity.X, JumpSpeed, Velocity.Z);
            IsGrounded = false;
        }

        if (!IsGrounded)
        {
            float verticalVelocity = Velocity.Y - Gravity * dt;
            Velocity = new Vector3(Velocity.X, MathF.Max(verticalVelocity, -TerminalVelocity), Velocity.Z);
        }

        Vector3 frameDelta = Velocity * dt;
        MoveAxis(world, frameDelta.X, Axis.X);
        MoveAxis(world, frameDelta.Z, Axis.Z);
        MoveAxis(world, frameDelta.Y, Axis.Y);

        CheckGrounded(world);
    }

    public void SyncCamera(Camera camera)
    {
        camera.Position = Position + new Vector3(0f, EyeHeight, 0f);
        camera.SetOrientation(Yaw, Pitch);
    }

    public static Vector3 GetColliderMin(Vector3 position)
        => new(position.X - ColliderRadius, position.Y, position.Z - ColliderRadius);

    public static Vector3 GetColliderMax(Vector3 position)
        => new(position.X + ColliderRadius, position.Y + ColliderHeight, position.Z + ColliderRadius);

    private void ApplyLook(Vector2 lookDelta)
    {
        Yaw += lookDelta.X * MouseSensitivity;
        Pitch -= lookDelta.Y * MouseSensitivity;
        Pitch = Math.Clamp(Pitch, -MaxPitch, MaxPitch);
    }

    private Vector3 GetWishDirection(float moveForward, float moveRight)
    {
        Vector3 forward = new(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));
        Vector3 right = new(-forward.Z, 0f, forward.X);
        Vector3 wishDirection = forward * moveForward + right * moveRight;

        if (wishDirection.LengthSquared() > 1f)
            wishDirection = Vector3.Normalize(wishDirection);

        return wishDirection;
    }

    private void MoveAxis(World world, float amount, Axis axis)
    {
        if (MathF.Abs(amount) < float.Epsilon)
            return;

        Vector3 delta = axis switch
        {
            Axis.X => new Vector3(amount, 0f, 0f),
            Axis.Y => new Vector3(0f, amount, 0f),
            _ => new Vector3(0f, 0f, amount),
        };

        if (TryMove(world, delta))
            return;

        if ((axis == Axis.X || axis == Axis.Z) && TryStepMove(world, delta))
            return;

        ZeroVelocity(axis);
    }

    private bool TryMove(World world, Vector3 delta)
    {
        float distance = delta.Length();
        Vector3 direction = Vector3.Normalize(delta);
        float moved = 0f;
        Vector3 start = Position;

        while (moved < distance)
        {
            float step = MathF.Min(CollisionStepSize, distance - moved);
            Vector3 candidate = Position + direction * step;
            if (world.IntersectsSolidAabb(GetColliderMin(candidate), GetColliderMax(candidate)))
            {
                Position = start + direction * moved;
                return false;
            }

            Position = candidate;
            moved += step;
        }

        return true;
    }

    private bool TryStepMove(World world, Vector3 horizontalDelta)
    {
        if (!IsGrounded || world.IntersectsSolidAabb(GetColliderMin(Position + new Vector3(0f, MaxStepHeight, 0f)), GetColliderMax(Position + new Vector3(0f, MaxStepHeight, 0f))))
            return false;

        Vector3 originalPosition = Position;
        Position += new Vector3(0f, MaxStepHeight, 0f);

        if (!TryMove(world, horizontalDelta))
        {
            Position = originalPosition;
            return false;
        }

        SettleDown(world, MaxStepHeight + GroundProbeDistance);
        return true;
    }

    private void SettleDown(World world, float maxDistance)
    {
        float moved = 0f;

        while (moved < maxDistance)
        {
            float step = MathF.Min(GroundSettleStepSize, maxDistance - moved);
            Vector3 candidate = Position - new Vector3(0f, step, 0f);

            if (world.IntersectsSolidAabb(GetColliderMin(candidate), GetColliderMax(candidate)))
                break;

            Position = candidate;
            moved += step;
        }
    }

    private void CheckGrounded(World world)
    {
        bool grounded = Velocity.Y <= 0f && world.IntersectsSolidAabb(
            GetColliderMin(Position - new Vector3(0f, GroundProbeDistance, 0f)),
            GetColliderMax(Position - new Vector3(0f, GroundProbeDistance, 0f)));

        IsGrounded = grounded;

        if (IsGrounded && Velocity.Y < 0f)
            Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
    }

    private void ZeroVelocity(Axis axis)
    {
        Velocity = axis switch
        {
            Axis.X => new Vector3(0f, Velocity.Y, Velocity.Z),
            Axis.Y => new Vector3(Velocity.X, 0f, Velocity.Z),
            _ => new Vector3(Velocity.X, Velocity.Y, 0f),
        };
    }

    private static Vector2 Accelerate(Vector2 current, Vector2 target, float acceleration, float dt)
    {
        Vector2 delta = target - current;
        float maxChange = acceleration * dt;

        if (delta.LengthSquared() <= maxChange * maxChange)
            return target;

        return current + Vector2.Normalize(delta) * maxChange;
    }

    private static Vector2 ApplyFriction(Vector2 velocity, float friction, float dt)
    {
        float speed = velocity.Length();
        if (speed <= float.Epsilon)
            return Vector2.Zero;

        float newSpeed = MathF.Max(0f, speed - friction * dt);
        return velocity * (newSpeed / speed);
    }

    private enum Axis
    {
        X,
        Y,
        Z,
    }
}
