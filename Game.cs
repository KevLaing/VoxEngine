using System;
using System.Numerics;
using Silk.NET.OpenGL;
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

    private float _targetAlteredState;
    private float _currentAlteredState;

    public Game(IWindow window)
    {
        _window = window;
        _camera = new Camera(new Vector3(30, 20, 30), Vector3.Zero);
        _world = new World(DateOnly.FromDateTime(DateTime.Now).DayNumber);
        _renderer = new VoxelRenderer(window);
        _inputController = new InputController(window, _camera, ToggleAlteredState);
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

        _world.Update(_camera.Position, _renderer.Gl);
    }

    private void OnUpdate(double delta)
    {
        _inputController.Update(delta);
        _world.Update(_camera.Position, _renderer.Gl);
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

    public void Dispose()
    {
        _renderer.Dispose();
        _inputController.Dispose();
    }
}
