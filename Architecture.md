Project: State-Driven Voxel Farming Engine
Tech Stack: C# (Standard 8.0+), Silk.NET, OpenGL 4.5.
Core Mechanics:

Voxel Storage: Bit-packed uint arrays (Type, Growth, Moisture).

Rendering: Greedy Meshing for terrain; GPU Instancing for crops.

Altered State: Global Shader Uniform (uAlteredState) controlling:

Palette Swap: Via 1D Texture LUT.

Model Swap: Via Instance ID offsets in the Vertex Shader.

Camera: Orbit/Pan with Y-axis "Slicing" for multi-level visibility.

Math: Deterministic Random and world seeding is required.