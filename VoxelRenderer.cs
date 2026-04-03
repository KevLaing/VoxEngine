using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using VoxEngine.Utils;

namespace VoxEngine;

public sealed class VoxelRenderer : IDisposable
{
    private readonly IWindow _window;

    private uint _program;
    private int _uMvpLocation;
    private int _uAlteredStateLocation;

    public GL Gl { get; private set; } = null!;

    public VoxelRenderer(IWindow window)
    {
        _window = window;
    }

    public unsafe void Initialize()
    {
        Gl = _window.CreateOpenGL();

        Gl.Viewport(0, 0, (uint)_window.Size.X, (uint)_window.Size.Y);
        Gl.Enable(EnableCap.DepthTest);
        Gl.Enable(EnableCap.Blend);
        Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        Gl.ClearColor(0.2f, 0.3f, 0.5f, 1.0f);

        _window.FramebufferResize += size =>
            Gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);

        string vertexShaderSource = File.ReadAllText("shaders/voxel.vert");
        string fragmentShaderSource = File.ReadAllText("shaders/voxel.frag");

        uint vertexShader = ShaderHelper.Compile(Gl, vertexShaderSource, ShaderType.VertexShader);
        uint fragmentShader = ShaderHelper.Compile(Gl, fragmentShaderSource, ShaderType.FragmentShader);

        _program = Gl.CreateProgram();
        Gl.AttachShader(_program, vertexShader);
        Gl.AttachShader(_program, fragmentShader);
        Gl.LinkProgram(_program);

        Gl.DeleteShader(vertexShader);
        Gl.DeleteShader(fragmentShader);

        _uMvpLocation = Gl.GetUniformLocation(_program, "uMVP");
        _uAlteredStateLocation = Gl.GetUniformLocation(_program, "uAlteredState");
    }

    public unsafe void Render(IEnumerable<Chunk> chunks, Camera camera, float alteredState)
    {
        Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        Gl.UseProgram(_program);

        Matrix4x4 view = camera.GetViewMatrix();
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            75f * (MathF.PI / 180f),
            (float)_window.Size.X / _window.Size.Y,
            0.1f,
            175f);

        Matrix4x4 viewProjection = view * projection;

        Gl.UniformMatrix4(_uMvpLocation, 1, false, (float*)&viewProjection);
        Gl.Uniform1(_uAlteredStateLocation, alteredState);

        int visibleChunks = 0;
        int culledChunks = 0;

        foreach (Chunk chunk in chunks)
        {
            if (!FrustumCuller.IntersectsAabb(viewProjection, chunk.BoundsMin, chunk.BoundsMax))
            {
                culledChunks++;
                continue;
            }

            visibleChunks++;

            if (!chunk.HasBuiltMesh)
                continue;

            Gl.BindVertexArray(chunk.VAO);
            Gl.DrawElements(
                PrimitiveType.Triangles,
                chunk.IndexCount,
                DrawElementsType.UnsignedInt,
                null);
        }

        Console.WriteLine($"Visible: {visibleChunks}, Culled: {culledChunks}");
    }

    public void Dispose()
    {
        if (Gl is not null && _program != 0)
        {
            Gl.DeleteProgram(_program);
        }
    }
}
