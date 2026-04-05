namespace VoxEngine.Utils;

public readonly record struct TerrainSample(
    float Temperature,
    float Moisture,
    float MountainDistance01,
    float MountainMask,
    float BaseTerrain,
    float Detail,
    float FjordMask,
    float RiverMask,
    int WaterSurfaceHeight,
    int SurfaceHeight,
    BiomeType Biome);
