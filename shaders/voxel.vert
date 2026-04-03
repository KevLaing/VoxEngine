#version 330 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in uint aVoxelData;

uniform mat4 uMVP;

flat out uint vType;
flat out float vGrowth;
flat out float vMoisture;
out float vWorldY;

void main()
{
    gl_Position = uMVP * vec4(aPos, 1.0);
    vWorldY = aPos.y;

    vType = aVoxelData & 0xFFFFu;

    uint growthRaw = (aVoxelData >> 16) & 0xFFu;
    vGrowth = float(growthRaw) / 255.0;

    uint moistureRaw = (aVoxelData >> 24) & 0xFFu;
    vMoisture = float(moistureRaw) / 255.0;
}