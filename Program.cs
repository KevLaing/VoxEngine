using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Input;
using System.Numerics; // Required for Matrix4x4
using System.Drawing;
using VoxEngine.Utils; // Import our new namespace

// 1. Setup Window Options
var options = WindowOptions.Default;
options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
options.Title = "Voxel Engine: Altered State Prototype";

using var window = Window.Create(options);
GL gl = null!;
IInputContext input = null!;
Camera camera = new Camera(new Vector3(30, 20, 30), Vector3.Zero);
float targetAlteredState = 0f;
float currentAlteredState = 0f;

void ToggleAlteredState() => targetAlteredState = targetAlteredState == 0f ? 1f : 0f;

window.Load += () => {
    // Initialize Input
    input = window.CreateInput();
    if (input.Keyboards.Count > 0)
    {
        input.Keyboards[0].KeyDown += (kb, key, code) => {
            if (key == Key.Space) ToggleAlteredState();
        };
    }

    unsafe
    {
        gl = window.CreateOpenGL();
    
    // --- SHADER SOURCE ---
    // Vertex Shader: Basic positioning
    const string vShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPos;         // Mesh Vertex
        layout (location = 1) in vec3 aInstancePos; // Instanced World Position
        layout (location = 2) in uint aVoxelData;   // Packed Voxel Data

        uniform mat4 uMVP; // Model-View-Projection Matrix

        flat out uint vType;
        flat out float vGrowth;
        flat out float vMoisture;

        void main() { 
            // Vertex pos (-0.5 to 0.5) + Instance World Pos
            vec3 worldPos = aPos + aInstancePos; 
            gl_Position = uMVP * vec4(worldPos, 1.0); 

            // --- UNPACKING LOGIC ---
            // Type: Bottom 16 bits
            vType = aVoxelData & 0xFFFFu;
            
            // Growth: Next 8 bits (Shift right 16, mask 0xFF)
            uint growthRaw = (aVoxelData >> 16) & 0xFFu;
            vGrowth = float(growthRaw) / 255.0; // Normalize 0..1

            // Moisture: Top 8 bits (Shift right 24, mask 0xFF)
            uint moistureRaw = (aVoxelData >> 24) & 0xFFu;
            vMoisture = float(moistureRaw) / 255.0; // Normalize 0..1
        }";

    // Fragment Shader: The "Altered State" Logic
    const string fShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        
        flat in uint vType;
        flat in float vGrowth;
        flat in float vMoisture;

        uniform float uAlteredState; // 0.0 to 1.0
        
        // Helper to rotate hue while preserving value/saturation
        vec3 hueShift(vec3 color, float hue) {
            const vec3 k = vec3(0.57735);
            float cosAngle = cos(hue);
            return color * cosAngle + cross(k, color) * sin(hue) + k * dot(k, color) * (1.0 - cosAngle);
        }

        void main() {
            // Base color determined by Voxel Type
            vec3 baseColor;
            if (vType == 1u) baseColor = vec3(0.6, 0.4, 0.2); // Dirt/Soil
            else baseColor = vec3(0.2, 0.6, 0.2);             // Grass

            // Visualize Growth (Brightness) and Moisture (Blue Tint)
            vec3 normalColor = baseColor * (0.5 + 0.5 * vGrowth); 
            normalColor = mix(normalColor, vec3(0.0, 0.0, 1.0), vMoisture * 0.5);

            // Debug: Shift the existing data along the color spectrum
            vec3 alteredColor = hueShift(normalColor, uAlteredState * 6.28318);
            
            // Linear interpolation between palettes
            vec3 finalColor = mix(normalColor, alteredColor, uAlteredState);
            
            // Add a subtle 'pulse' effect if in altered state
            if(uAlteredState > 0.1) {
                finalColor *= 0.8 + 0.2 * sin(uAlteredState * 5.0);
            }
            
            FragColor = vec4(finalColor, 1.0);
        }";

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

    // 2. Generate 3D Chunk (Instances)
    int gridSize = 32;
    var posList = new List<float>();
    var dataList = new List<uint>();

    // Deterministic Random & Noise
    var random = new Random(1337); // Fixed seed
    var perlin = new Perlin(random);

    for (int x = 0; x < gridSize; x++)
    {
        for (int z = 0; z < gridSize; z++)
        {
            // Use Noise to determine terrain height (Heightmap)
            // Map noise (-1 to 1) to height (1 to 10)
            double nHeight = perlin.Noise(x * 0.1, z * 0.1, 0.0);
            int height = (int)((nHeight * 0.5 + 0.5) * 10) + 2;

            for (int y = 0; y < height; y++)
            {
                posList.Add(x - gridSize / 2f);
                posList.Add(y - 5f);
                posList.Add(z - gridSize / 2f);

                // Logic: Top block is Grass (0), below is Dirt (1)
                uint type = (y == height - 1) ? 0u : 1u;
            
                // 3D Noise for variation
                double n3d = perlin.Noise(x * 0.15, y * 0.15, z * 0.15);
                uint growth = (uint)((Math.Sin(n3d * 10) * 0.5 + 0.5) * 255);
                uint moisture = (uint)((n3d * 0.5 + 0.5) * 255);

                dataList.Add(new Voxel(type, growth, moisture).Data);
            }
        }
    }
    
    float[] instancePositions = posList.ToArray();
    uint[] instanceData = dataList.ToArray();
    uint instanceCount = (uint)instanceData.Length;

    uint vertexArrayOutput = gl.GenVertexArray();
    gl.BindVertexArray(vertexArrayOutput);
    uint vertexBufferOutput = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferOutput);
    fixed (float* v = vertices) gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
    gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
    gl.EnableVertexAttribArray(0);
    uint vertexBufferOutputPos = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferOutputPos);
    fixed (float* p = instancePositions) gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(instancePositions.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
    gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
    gl.VertexAttribDivisor(1, 1); // Update once per instance
    gl.EnableVertexAttribArray(1);

    // vertexBufferOutput 3: Voxel Data (UINT)
    uint vertexBufferOutputData = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferOutputData);
    fixed (uint* d = instanceData) gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(instanceData.Length * sizeof(uint)), d, BufferUsageARB.StaticDraw);
    // IMPORTANT: VertexAttribIPointer for Integers
    gl.VertexAttribIPointer(2, 1, VertexAttribIType.UnsignedInt, sizeof(uint), (void*)0); 
    gl.VertexAttribDivisor(2, 1); // Update once per instance
    gl.EnableVertexAttribArray(2);

    // EBO: Indices
    uint ebo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
    fixed (uint* i = indices) gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);

    // --- RENDER LOOP ---
    window.Render += (double delta) => {
        gl.Clear(ClearBufferMask.ColorBufferBit);
        
        // Smoothly interpolate the state for visual feedback
        float lerpSpeed = 5.0f;
        currentAlteredState += (targetAlteredState - currentAlteredState) * (float)delta * lerpSpeed;

        int loc = gl.GetUniformLocation(program, "uAlteredState");
        gl.Uniform1(loc, currentAlteredState);

        // Camera Matrix
        var view = camera.GetViewMatrix();
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, 800f / 600f, 0.1f, 100f);
        var mvp = view * proj;

        int uMVP = gl.GetUniformLocation(program, "uMVP");
        gl.UniformMatrix4(uMVP, 1, false, (float*)&mvp);

        gl.BindVertexArray(vertexArrayOutput);
        // Instanced Draw: 36 indices per cube
        gl.DrawElementsInstanced(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, (void*)0, instanceCount);
    };
    }
};

window.Update += (double delta) => {
    if (input != null && input.Keyboards.Count > 0)
    {
        camera.OnUpdate(delta, input.Keyboards[0]);
    }
};

window.Run();