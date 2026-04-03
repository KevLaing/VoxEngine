using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Input;
using System.Numerics; // Required for Matrix4x4
using System.Drawing;
using System.IO;
using VoxEngine.Utils;
using System.ComponentModel; // Import our new namespace

// 1. Setup Window Options
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
float[] instancePositions = null!;
uint[] instanceData = null!;
uint instanceCount = 0;

bool worldDirty = true; // Flag to track if world data has changed
void ToggleAlteredState() => targetAlteredState = targetAlteredState == 0f ? 1f : 0f;

window.Load += () =>
{
    // Initialize Input
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

        // Initialize the viewport to the actual window size
        gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);

        // Fix: Enable Depth Testing so voxels render in the correct Z-order
        gl.Enable(EnableCap.DepthTest);

        // Enable Blending for transparent water
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Update viewport if the window is resized or resolution changes
        window.FramebufferResize += s => gl.Viewport(0, 0, (uint)s.X, (uint)s.Y);

        // Load shaders from files
        string vShaderSource = File.ReadAllText("shaders/voxel.vert");
        string fShaderSource = File.ReadAllText("shaders/voxel.frag");

        // --- COMPILE & LINK ---
        uint vShader = ShaderHelper.Compile(gl, vShaderSource, ShaderType.VertexShader);
        uint fShader = ShaderHelper.Compile(gl, fShaderSource, ShaderType.FragmentShader);
        uint program = gl.CreateProgram();
        gl.AttachShader(program, vShader);
        gl.AttachShader(program, fShader);
        gl.LinkProgram(program);
        gl.UseProgram(program);

        // --- GEOMETRY GENERATION ---
        // 1. Mesh (Simple Cube, size 1.0)
        float[] vertices = {
        -0.5f, -0.5f, -0.5f,  0.5f, -0.5f, -0.5f,  0.5f,  0.5f, -0.5f, -0.5f,  0.5f, -0.5f, // Back
        -0.5f, -0.5f,  0.5f,  0.5f, -0.5f,  0.5f,  0.5f,  0.5f,  0.5f, -0.5f,  0.5f,  0.5f, // Front
    };
        uint[] indices = {
        0, 1, 2, 2, 3, 0,       // Back
        4, 5, 6, 6, 7, 4,       // Front
        4, 0, 3, 3, 7, 4,       // Left
        1, 5, 6, 6, 2, 1,       // Right
        3, 2, 6, 6, 7, 3,       // Top
        4, 5, 1, 1, 0, 4        // Bottom
    };

        // 2. Initialize World Manager
        world = new World(DateOnly.FromDateTime(DateTime.Now).DayNumber);
        world.Update(camera.Position);
        worldDirty = true; // Initial load done, reset dirty flag


        uint vertexArrayOutput = gl.GenVertexArray();
        gl.BindVertexArray(vertexArrayOutput);
        uint vertexBufferOutput = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferOutput);
        fixed (float* v = vertices) gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.DynamicDraw);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);
        uint vertexBufferOutputPos = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferOutputPos);
        gl.BufferData(BufferTargetARB.ArrayBuffer, 0, null, BufferUsageARB.DynamicDraw);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        gl.VertexAttribDivisor(1, 1); // Update once per instance
        gl.EnableVertexAttribArray(1);

        // vertexBufferOutput 3: Voxel Data (UINT)
        uint vertexBufferOutputData = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferOutputData);
        gl.BufferData(BufferTargetARB.ArrayBuffer, 0, null, BufferUsageARB.DynamicDraw);
        // IMPORTANT: VertexAttribIPointer for Integers
        gl.VertexAttribIPointer(2, 1, VertexAttribIType.UnsignedInt, sizeof(uint), (void*)0);
        gl.VertexAttribDivisor(2, 1); // Update once per instance
        gl.EnableVertexAttribArray(2);

        // EBO: Indices
        uint ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* i = indices) gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.DynamicDraw);

        // --- RENDER LOOP ---
        window.Render += (double delta) =>
        {
            // Fix: Clear both Color AND Depth buffers every frame
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            // Temporary: Collect initial data to setup buffers
            // In a final engine, you'd use a dynamic buffer or separate VAOs per chunk.
             // Camera Matrix
            var view = camera.GetViewMatrix();
            // Set FOV to 75 degrees (converted to radians) for a natural perspective
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(75f * (MathF.PI / 180f), (float)window.Size.X / window.Size.Y, 0.1f, 100f);
            var mvp = view * proj;

            int uMVP = gl.GetUniformLocation(program, "uMVP");
            // Transpose set to false per local coordinate system requirements
            gl.UniformMatrix4(uMVP, 1, false, (float*)&mvp);

            foreach (var chunk in world.GetActiveChunks())
            {
                if (chunk.IsDirty)
                    chunk.BuildMesh(gl, vertices, indices);

                if (chunk.InstanceCount == 0)
                    continue;

                gl.BindVertexArray(chunk.VAO);
                gl.DrawElementsInstanced(
                    PrimitiveType.Triangles,
                    36,
                    DrawElementsType.UnsignedInt,
                    (void*)0,
                    chunk.InstanceCount
                );
            }


            // Smoothly interpolate the state for visual feedback
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
    {
        camera.OnUpdate(delta, input.Keyboards[0]);
    }

    bool changed = world.Update(camera.Position);

    if (changed)
        worldDirty = true;
};

window.Run();