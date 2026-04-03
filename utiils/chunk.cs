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
    public uint InstanceVBO;
    public uint DataVBO;
    public uint InstanceCount;
    public bool IsDirty = true;

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

    public unsafe void BuildMesh(GL gl, uint cubeVBO, uint sharedEBO)
    {
        var posList = new List<float>();
        var dataList = new List<uint>();

        for (int x = 0; x < SizeX; x++)
        for (int z = 0; z < SizeZ; z++)
        for (int y = 0; y < Height; y++)
        {
            var voxel = Voxels[x + SizeX * (y + Height * z)];
            if (voxel.Data == 0) continue;

            posList.Add(ChunkX * SizeX + x);
            posList.Add(y);
            posList.Add(ChunkZ * SizeZ + z);
            dataList.Add(voxel.Data);
        }

        InstanceCount = (uint)dataList.Count;

        if (InstanceCount == 0)
        {
            IsDirty = false;
            return;
        }

        if (VAO == 0)
        {
            VAO = gl.GenVertexArray();
            gl.BindVertexArray(VAO);

            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, sharedEBO);

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, cubeVBO);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);

            InstanceVBO = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVBO);
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
            gl.VertexAttribDivisor(1, 1);
            gl.EnableVertexAttribArray(1);

            DataVBO = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, DataVBO);
            gl.VertexAttribIPointer(2, 1, VertexAttribIType.UnsignedInt, sizeof(uint), (void*)0);
            gl.VertexAttribDivisor(2, 1);
            gl.EnableVertexAttribArray(2);
        }

        gl.BindVertexArray(VAO);

        var posArray = posList.ToArray();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVBO);
        fixed (float* p = posArray)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(posArray.Length * sizeof(float)),
                p,
                BufferUsageARB.DynamicDraw);
        }

        var dataArray = dataList.ToArray();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, DataVBO);
        fixed (uint* d = dataArray)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(dataArray.Length * sizeof(uint)),
                d,
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