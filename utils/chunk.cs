using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace VoxEngine.Utils;

public class Chunk
{
    public const int SizeX = 32;
    public const int SizeZ = 32;
    public const int Height = 512;

    public int ChunkX { get; }
    public int ChunkZ { get; }
    public Voxel[] Voxels { get; }

    public Vector3 BoundsMin { get; }
    public Vector3 BoundsMax { get; }

    public uint VAO;
    public uint VertexVBO;
    public uint DataVBO;
    public uint EBO;
    public uint IndexCount;
    public bool IsDirty = true;
    public bool HasBuiltMesh => VAO != 0 && IndexCount > 0;

    //meshing helpers

    private int IndexOf(int x, int y, int z)
    => x + SizeX * (y + Height * z);

    public bool InBounds(int x, int y, int z)
        => x >= 0 && x < SizeX &&
           y >= 0 && y < Height &&
           z >= 0 && z < SizeZ;

    public bool IsSolidLocal(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return false;
        uint voxelType = Voxels[IndexOf(x, y, z)].Type;
        return voxelType != 0 && voxelType != World.WaterVoxelType;
    }

    private uint GetVoxelTypeWorld(World world, int localX, int localY, int localZ)
    {
        if (localY < 0 || localY >= Height)
            return 0;

        if (localX >= 0 && localX < SizeX &&
            localZ >= 0 && localZ < SizeZ)
        {
            return Voxels[IndexOf(localX, localY, localZ)].Type;
        }

        int worldX = ChunkX * SizeX + localX;
        int worldZ = ChunkZ * SizeZ + localZ;

        return world.GetVoxelType(worldX, localY, worldZ);
    }
    public uint GetVoxelDataLocal(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return 0;
        return Voxels[IndexOf(x, y, z)].Data;
    }
    private static void AddQuad(
    List<float> vertices,
    List<uint> voxelData,
    List<uint> indices,
    Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
    uint data)
    {
        uint baseIndex = (uint)(vertices.Count / 3);

        vertices.Add(v0.X); vertices.Add(v0.Y); vertices.Add(v0.Z);
        vertices.Add(v1.X); vertices.Add(v1.Y); vertices.Add(v1.Z);
        vertices.Add(v2.X); vertices.Add(v2.Y); vertices.Add(v2.Z);
        vertices.Add(v3.X); vertices.Add(v3.Y); vertices.Add(v3.Z);

        voxelData.Add(data);
        voxelData.Add(data);
        voxelData.Add(data);
        voxelData.Add(data);

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
        indices.Add(baseIndex + 0);
    }
    // Chunk constructor
    public Chunk(int cx, int cz, int worldSeed, TerrainSampler terrainSampler)
    {
        ChunkX = cx;
        ChunkZ = cz;
        Voxels = new Voxel[SizeX * SizeZ * Height];

        BoundsMin = new Vector3(ChunkX * SizeX, 0, ChunkZ * SizeZ);
        BoundsMax = new Vector3((ChunkX + 1) * SizeX, Height, (ChunkZ + 1) * SizeZ);

        int chunkSeed = worldSeed ^ (cx * 73856093) ^ (cz * 19349663);
        var rand = new DeterministicRandom(chunkSeed);

        Generate(terrainSampler, rand);
    }

    public unsafe void BuildMesh(GL gl, World world)
    {
        var vertices = new List<float>();
        var voxelData = new List<uint>();
        var indices = new List<uint>();

        for (int x = 0; x < SizeX; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < SizeZ; z++)
                {
                    uint data = GetVoxelDataLocal(x, y, z);
                    if (data == 0) continue;
                    uint voxelType = data & 0xFFFFu;

                    float wx = ChunkX * SizeX + x;
                    float wy = y;
                    float wz = ChunkZ * SizeZ + z;

                    // -X
                    if (ShouldRenderFace(voxelType, GetVoxelTypeWorld(world, x - 1, y, z), FaceDirection.Side))
                    {
                        AddQuad(
                            vertices, voxelData, indices,
                            new Vector3(wx, wy, wz),
                            new Vector3(wx, wy + 1, wz),
                            new Vector3(wx, wy + 1, wz + 1),
                            new Vector3(wx, wy, wz + 1),
                            data);
                    }

                    // +X
                    if (ShouldRenderFace(voxelType, GetVoxelTypeWorld(world, x + 1, y, z), FaceDirection.Side))
                    {
                        AddQuad(
                            vertices, voxelData, indices,
                            new Vector3(wx + 1, wy, wz + 1),
                            new Vector3(wx + 1, wy + 1, wz + 1),
                            new Vector3(wx + 1, wy + 1, wz),
                            new Vector3(wx + 1, wy, wz),
                            data);
                    }

                    // -Y
                    if (ShouldRenderFace(voxelType, GetVoxelTypeWorld(world, x, y - 1, z), FaceDirection.Bottom))
                    {
                        AddQuad(
                            vertices, voxelData, indices,
                            new Vector3(wx, wy, wz + 1),
                            new Vector3(wx + 1, wy, wz + 1),
                            new Vector3(wx + 1, wy, wz),
                            new Vector3(wx, wy, wz),
                            data);
                    }

                    // +Y
                    if (ShouldRenderFace(voxelType, GetVoxelTypeWorld(world, x, y + 1, z), FaceDirection.Top))
                    {
                        AddQuad(
                            vertices, voxelData, indices,
                            new Vector3(wx, wy + 1, wz),
                            new Vector3(wx + 1, wy + 1, wz),
                            new Vector3(wx + 1, wy + 1, wz + 1),
                            new Vector3(wx, wy + 1, wz + 1),
                            data);
                    }

                    // -Z
                    if (ShouldRenderFace(voxelType, GetVoxelTypeWorld(world, x, y, z - 1), FaceDirection.Side))
                    {
                        AddQuad(
                            vertices, voxelData, indices,
                            new Vector3(wx + 1, wy, wz),
                            new Vector3(wx + 1, wy + 1, wz),
                            new Vector3(wx, wy + 1, wz),
                            new Vector3(wx, wy, wz),
                            data);
                    }

                    // +Z
                    if (ShouldRenderFace(voxelType, GetVoxelTypeWorld(world, x, y, z + 1), FaceDirection.Side))
                    {
                        AddQuad(
                            vertices, voxelData, indices,
                            new Vector3(wx, wy, wz + 1),
                            new Vector3(wx, wy + 1, wz + 1),
                            new Vector3(wx + 1, wy + 1, wz + 1),
                            new Vector3(wx + 1, wy, wz + 1),
                            data);
                    }
                }
            }
        }

        IndexCount = (uint)indices.Count;

        if (IndexCount == 0)
        {
            IsDirty = false;
            return;
        }

        if (VAO == 0)
        {
            VAO = gl.GenVertexArray();
            VertexVBO = gl.GenBuffer();
            DataVBO = gl.GenBuffer();
            EBO = gl.GenBuffer();

            gl.BindVertexArray(VAO);

            // position attribute
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, VertexVBO);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);

            // packed voxel data attribute
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, DataVBO);
            gl.VertexAttribIPointer(1, 1, VertexAttribIType.UnsignedInt, sizeof(uint), (void*)0);
            gl.EnableVertexAttribArray(1);

            // element buffer
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, EBO);
        }

        gl.BindVertexArray(VAO);

        var vertexArray = vertices.ToArray();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, VertexVBO);
        fixed (float* v = vertexArray)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertexArray.Length * sizeof(float)),
                v,
                BufferUsageARB.DynamicDraw);
        }

        var dataArray = voxelData.ToArray();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, DataVBO);
        fixed (uint* d = dataArray)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(dataArray.Length * sizeof(uint)),
                d,
                BufferUsageARB.DynamicDraw);
        }

        var indexArray = indices.ToArray();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, EBO);
        fixed (uint* i = indexArray)
        {
            gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(indexArray.Length * sizeof(uint)),
                i,
                BufferUsageARB.DynamicDraw);
        }

        IsDirty = false;
    }

    public void ReleaseMesh(GL gl)
    {
        if (VAO != 0)
        {
            gl.DeleteVertexArray(VAO);
            VAO = 0;
        }

        if (VertexVBO != 0)
        {
            gl.DeleteBuffer(VertexVBO);
            VertexVBO = 0;
        }

        if (DataVBO != 0)
        {
            gl.DeleteBuffer(DataVBO);
            DataVBO = 0;
        }

        if (EBO != 0)
        {
            gl.DeleteBuffer(EBO);
            EBO = 0;
        }

        IndexCount = 0;
    }


    private void Generate(TerrainSampler terrainSampler, DeterministicRandom rand)
    {
        for (int x = 0; x < SizeX; x++)
        {
            for (int z = 0; z < SizeZ; z++)
            {
                int worldX = (ChunkX * SizeX) + x;
                int worldZ = (ChunkZ * SizeZ) + z;
                TerrainSample sample = terrainSampler.SampleColumn(worldX, worldZ);
                int terrainHeight = sample.SurfaceHeight;
                int waterHeight = sample.WaterSurfaceHeight;
                byte moisture = (byte)Math.Clamp((int)(sample.Moisture * 255f), 0, 255);

                for (int y = 0; y < Height; y++)
                {
                    uint type = 0;

                    if (y < terrainHeight)
                    {
                        type = SelectTerrainMaterial(sample, terrainHeight, y);
                    }
                    else if (y < waterHeight)
                    {
                        type = 3u;
                    }
                    else break;

                    byte growth = (byte)rand.Next(256);
                    Voxels[x + SizeX * (y + Height * z)] = new Voxel(type, growth, moisture);
                }
            }
        }

        IsDirty = true;
    }

    private static bool ShouldRenderFace(uint voxelType, uint neighborType, FaceDirection direction)
    {
        bool isWater = voxelType == World.WaterVoxelType;
        bool neighborIsAir = neighborType == 0;
        bool neighborIsWater = neighborType == World.WaterVoxelType;

        if (isWater)
        {
            if (direction == FaceDirection.Bottom)
                return neighborIsAir;

            return !neighborIsWater;
        }

        return neighborIsAir || neighborIsWater;
    }

    private static uint SelectTerrainMaterial(TerrainSample sample, int terrainHeight, int y)
    {
        int depthFromSurface = terrainHeight - 1 - y;
        bool isSurface = depthFromSurface == 0;
        bool isShoreline = terrainHeight <= sample.WaterSurfaceHeight + 2;
        bool isRiverBank = sample.RiverMask > 0.20f && terrainHeight <= sample.WaterSurfaceHeight + 3;
        bool isSnow = ShouldPlaceSnow(sample, terrainHeight, depthFromSurface);

        return sample.Biome switch
        {
            BiomeType.Forest => isSurface ? ((isShoreline || isRiverBank) ? 4u : 1u) : 2u,
            BiomeType.Plains => isSurface ? ((sample.Moisture < 0.38f || isShoreline || isRiverBank) ? 4u : 1u) : 2u,
            BiomeType.RockyFoothills => isSurface && depthFromSurface <= 1 && !isShoreline ? 1u : 2u,
            BiomeType.Mountain => isSnow ? 5u : 2u,
            BiomeType.Fjord => isSurface ? 4u : 2u,
            _ => 2u,
        };
    }

    private static bool ShouldPlaceSnow(TerrainSample sample, int terrainHeight, int depthFromSurface)
    {
        if (sample.Biome != BiomeType.Mountain)
            return false;

        float snowLine = 190f - sample.Moisture * 18f - (1f - sample.Temperature) * 16f;
        float snowCoverage = Math.Clamp((terrainHeight - snowLine) / 70f, 0f, 1f);

        if (snowCoverage <= 0f)
            return false;

        if (depthFromSurface == 0)
            return snowCoverage > 0.12f;

        return depthFromSurface == 1 && snowCoverage > 0.70f;
    }

    private enum FaceDirection
    {
        Side,
        Top,
        Bottom,
    }
}
