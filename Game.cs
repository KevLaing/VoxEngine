using System;
using System.Numerics;
using Silk.NET.Windowing;
using VoxEngine.Utils;

namespace VoxEngine;

public sealed class Game : IDisposable
{
    private readonly IWindow _window;
    private readonly Camera _camera;
    private readonly World _world;
    private readonly VoxelRenderer _renderer;
    private readonly InputController _inputController;
    private readonly ChunkMeshingScheduler _meshingScheduler;
    private PlayerController? _player;

    private float _targetAlteredState;
    private float _currentAlteredState;
    private static readonly Vector2 InitialSpawnXZ = new(30f, 30f);

    public Game(IWindow window)
    {
        _window = window;
        _camera = new Camera(new Vector3(30, 20, 30), Vector3.Zero);
        _world = new World(DateOnly.FromDateTime(DateTime.Now).DayNumber);
        _renderer = new VoxelRenderer(window);
        _inputController = new InputController(window, ToggleAlteredState);
        _meshingScheduler = new ChunkMeshingScheduler(1);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
    }

    public void Run() => _window.Run();

    private void OnLoad()
    {
        _inputController.Initialize();
        _renderer.Initialize();

        _world.Update(new Vector3(InitialSpawnXZ.X, 0f, InitialSpawnXZ.Y), _renderer.Gl);
        _player = CreatePlayer();
        _player.SyncCamera(_camera);
    }

    private void OnUpdate(double delta)
    {
        if (_player is null)
            return;

        PlayerInput input = _inputController.BuildPlayerInput();
        _player.Update(_world, input, (float)delta);
        _player.SyncCamera(_camera);

        _world.Update(_player.Position, _renderer.Gl);
        _meshingScheduler.Process(_renderer.Gl, _world);

        const float lerpSpeed = 5.0f;
        _currentAlteredState += (_targetAlteredState - _currentAlteredState) * (float)delta * lerpSpeed;
    }

    private void OnRender(double delta)
    {
        _renderer.Render(_world.GetActiveChunks(), _camera, _currentAlteredState);
    }

    private void ToggleAlteredState()
    {
        _targetAlteredState = _targetAlteredState == 0f ? 1f : 0f;
    }

    private PlayerController CreatePlayer()
    {
        int worldX = (int)MathF.Floor(InitialSpawnXZ.X);
        int worldZ = (int)MathF.Floor(InitialSpawnXZ.Y);
        float spawnY = _world.FindSurfaceY(worldX, worldZ) + 1f;
        Vector3 position = new(InitialSpawnXZ.X, spawnY, InitialSpawnXZ.Y);

        while (_world.IntersectsSolidAabb(
            PlayerController.GetColliderMin(position),
            PlayerController.GetColliderMax(position)))
        {
            position.Y += 1f;
        }

        return new PlayerController(position, -3f * MathF.PI / 4f, -0.35f);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        _inputController.Dispose();
    }
}
