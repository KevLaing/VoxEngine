using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Input;
using System.Numerics;
using System.IO;
using VoxEngine.Utils;

var options = WindowOptions.Default;
options.Size = new Silk.NET.Maths.Vector2D<int>(1920, 1080);
options.WindowState = WindowState.Fullscreen;
options.Title = "Voxel Engine: Altered State Prototype";

using var window = Window.Create(options);

GL gl = null!;
IInputContext input = null!;
Camera camera = new Camera(new Vector3(30, 20, 30), Vector3.Zero);

float targetAlteredState = 0f;
float currentAlteredState = 0f;

Vector2 lastMousePos;
World world = null!;

void ToggleAlteredState() =>
    targetAlteredState = targetAlteredState == 0f ? 1f : 0f;

window.Load += () =>
{
    input = window.CreateInput();

    if (input.Mice.Count > 0)
    {
        var mouse = input.Mice[0];
        mouse.Cursor.CursorMode = CursorMode.Disabled;

        lastMousePos = new Vector2(mouse.Position.X, mouse.Position.Y);

        mouse.MouseMove += (m, pos) =>
        {
            var delta = new Vector2(pos.X - lastMousePos.X, pos.Y - lastMousePos.Y);
            lastMousePos = new Vector2(pos.X, pos.Y);
            camera.OnMouseMove(delta);
        };
    }

    if (input.Keyboards.Count > 0)
    {
        input.Keyboards[0].KeyDown += (kb, key, code) =>
        {
            if (key == Key.U) ToggleAlteredState();
        };
    }

    unsafe
    {
        gl = window.CreateOpenGL();

        gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        window.FramebufferResize += s =>
            gl.Viewport(0, 0, (uint)s.X, (uint)s.Y);

        // --- SHADERS ---
        string vShaderSource = File.ReadAllText("shaders/voxel.vert");
        string fShaderSource = File.ReadAllText("shaders/voxel.frag");

        uint vShader = ShaderHelper.Compile(gl, vShaderSource, ShaderType.VertexShader);
        uint fShader = ShaderHelper.Compile(gl, fShaderSource, ShaderType.FragmentShader);

        uint program = gl.CreateProgram();
        gl.AttachShader(program, vShader);
        gl.AttachShader(program, fShader);
        gl.LinkProgram(program);
        gl.UseProgram(program);

        // --- SHARED CUBE MESH ---
        float[] vertices = {
            -0.5f,-0.5f,-0.5f,  0.5f,-0.5f,-0.5f,  0.5f, 0.5f,-0.5f, -0.5f, 0.5f,-0.5f,
            -0.5f,-0.5f, 0.5f,  0.5f,-0.5f, 0.5f,  0.5f, 0.5f, 0.5f, -0.5f, 0.5f, 0.5f,
        };

        uint[] indices = {
            0,1,2,2,3,0,
            4,5,6,6,7,4,
            4,0,3,3,7,4,
            1,5,6,6,2,1,
            3,2,6,6,7,3,
            4,5,1,1,0,4
        };

        uint sharedVAO = gl.GenVertexArray();
        gl.BindVertexArray(sharedVAO);

        uint cubeVBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, cubeVBO);
        fixed (float* v = vertices)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                v,
                BufferUsageARB.StaticDraw);
        }

        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);

        uint sharedEBO = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, sharedEBO);
        fixed (uint* i = indices)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);

        // --- WORLD ---
        world = new World(DateOnly.FromDateTime(DateTime.Now).DayNumber);
        world.Update(camera.Position);

        // --- RENDER ---
        window.Render += (double delta) =>
        {
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Camera
            var view = camera.GetViewMatrix();
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(
                75f * (MathF.PI / 180f),
                (float)window.Size.X / window.Size.Y,
                0.1f,
                175f);

            var mvp = view * proj;

            int uMVP = gl.GetUniformLocation(program, "uMVP");
            gl.UniformMatrix4(uMVP, 1, false, (float*)&mvp);

            // Render chunks
            foreach (var chunk in world.GetActiveChunks())
            {
                if (chunk.IsDirty)
                    chunk.BuildMesh(gl, cubeVBO, sharedEBO);

                if (chunk.InstanceCount == 0)
                    continue;

                gl.BindVertexArray(chunk.VAO);

                gl.DrawElementsInstanced(
                    PrimitiveType.Triangles,
                    36,
                    DrawElementsType.UnsignedInt,
                    (void*)0,
                    chunk.InstanceCount);
            }

            // Altered state
            float lerpSpeed = 5.0f;
            currentAlteredState += (targetAlteredState - currentAlteredState) * (float)delta * lerpSpeed;

            int loc = gl.GetUniformLocation(program, "uAlteredState");
            gl.Uniform1(loc, currentAlteredState);
        };
    }
};

window.Update += (double delta) =>
{
    if (input != null && input.Keyboards.Count > 0)
        camera.OnUpdate(delta, input.Keyboards[0]);

    world.Update(camera.Position);
};

window.Run();