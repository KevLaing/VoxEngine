using System.Numerics;

namespace VoxEngine;

public readonly struct PlayerInput
{
    public float MoveForward { get; init; }
    public float MoveRight { get; init; }
    public bool JumpPressed { get; init; }
    public bool SprintHeld { get; init; }
    public Vector2 LookDelta { get; init; }
}
