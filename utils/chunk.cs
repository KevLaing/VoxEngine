using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace VoxEngine.Utils;

public class Chunk
{
    public const int SizeX = 32;
    public const int SizeZ = 32;
    public const int Height = 128;

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
        return Voxels[IndexOf(x, y, z)].Data != 0;
    }
    private bool IsSolidWorld(World world, int localX, int localY, int localZ)
    {
        if (localY < 0 || localY >= Height)
            return false;

        if (localX >= 0 && localX < SizeX &&
            localZ >= 0 && localZ < SizeZ)
        {
            return Voxels[IndexOf(localX, localY, localZ)].Data != 0;
        }

        int worldX = ChunkX * SizeX + localX;
        int worldZ = ChunkZ * SizeZ + localZ;

        return world.IsSolid(worldX, localY, worldZ);
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
    public Chunk(int cx, int cz, int worldSeed, Perlin noise)
    {
        ChunkX = cx;
        ChunkZ = cz;
        Voxels = new Voxel[SizeX * SizeZ * Height];

        BoundsMin = new Vector3(ChunkX * SizeX, 0, ChunkZ * SizeZ);
        BoundsMax = new Vector3((ChunkX + 1) * SizeX, Height, (ChunkZ + 1) * SizeZ);

        int chunkSeed = worldSeed ^ (cx * 73856093) ^ (cz * 19349663);
        var rand = new DeterministicRandom(chunkSeed);

        Generate(noise, rand);
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

                    float wx = ChunkX * SizeX + x;
                    float wy = y;
                    float wz = ChunkZ * SizeZ + z;

                    // -X
                    if (!IsSolidWorld(world, x - 1, y, z))
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
                    if (!IsSolidWorld(world, x + 1, y, z))
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
                    if (!IsSolidWorld(world, x, y - 1, z))
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
                    if (!IsSolidWorld(world, x, y + 1, z))
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
                    if (!IsSolidWorld(world, x, y, z - 1))
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
                    if (!IsSolidWorld(world, x, y, z + 1))
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


    private void Generate(Perlin noise, DeterministicRandom rand)
    {
        int waterLevel = 28;

        for (int x = 0; x < SizeX; x++)
        {
            for (int z = 0; z < SizeZ; z++)
            {
                double worldX = (ChunkX * SizeX) + x;
                double worldZ = (ChunkZ * SizeZ) + z;

                double nHeight = noise.Noise(worldX * 0.025, worldZ * 0.025, 0.0);
                int terrainHeight = (int)((nHeight * 0.5 + 0.5) * 60) + 8;

                for (int y = 0; y < Height; y++)
                {
                    uint type = 0;

                    if (y < terrainHeight)
                    {
                        if (y == terrainHeight - 1)
                            type = (terrainHeight <= waterLevel + 1) ? 4u : 1u;
                        else
                            type = 2u;
                    }
                    else if (y < waterLevel)
                    {
                        type = 3u;
                    }
                    else break;

                    byte growth = (byte)rand.Next(256);
                    byte moisture = (byte)rand.Next(256);

                    Voxels[x + SizeX * (y + Height * z)] = new Voxel(type, growth, moisture);
                }
            }
        }

        IsDirty = true;
    }
}