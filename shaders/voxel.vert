#version 330 core
layout (location = 0) in vec3 aPos;         // Mesh Vertex
layout (location = 1) in vec3 aInstancePos; // Instanced World Position
layout (location = 2) in uint aVoxelData;   // Packed Voxel Data

uniform mat4 uMVP; // Model-View-Projection Matrix

flat out uint vType;
flat out float vGrowth;
flat out float vMoisture;
out float vWorldY;

void main() { 
    // Vertex pos (-0.5 to 0.5) + Instance World Pos
    vec3 worldPos = aPos + aInstancePos; 
    gl_Position = uMVP * vec4(worldPos, 1.0); 

    vWorldY = aInstancePos.y;

    // --- UNPACKING LOGIC ---
    // Type: Bottom 16 bits
    vType = aVoxelData & 0xFFFFu;
    
    // Growth: Next 8 bits (Shift right 16, mask 0xFF)
    uint growthRaw = (aVoxelData >> 16) & 0xFFu;
    vGrowth = float(growthRaw) / 255.0; // Normalize 0..1

    // Moisture: Top 8 bits (Shift right 24, mask 0xFF)
    uint moistureRaw = (aVoxelData >> 24) & 0xFFu;
    vMoisture = float(moistureRaw) / 255.0; // Normalize 0..1
}