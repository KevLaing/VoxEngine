using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;
using VoxEngine.Utils;


namespace VoxEngine.Utils;

public class Chunk
{
    public const int SizeX = 32;
    public const int SizeZ = 32;
    public const int Height = 128;

    public int ChunkX { get; }
    public int ChunkZ { get; }
    public Voxel[] Voxels { get; }

    // per chunk mesh data
    public uint VAO;  //stores attributes bindings
    public uint InstanceVBO;  //positions
    public uint DataVBO;  // voxel data
    public uint InstanceCount;  //how many voxels
    public bool IsDirty = true;  //neds rebuild



    public Chunk(int cx, int cz, int worldSeed, Perlin noise)
    {
        ChunkX = cx;
        ChunkZ = cz;
        Voxels = new Voxel[SizeX * SizeZ * Height];

        // Deterministic seed based on position
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
        //Console.WriteLine($"Chunk {ChunkX},{ChunkZ} instances: {InstanceCount}");

        if (InstanceCount == 0)
        {
            IsDirty = false;
            return;
        }

        // ✅ Create VAO ONCE per chunk
        if (VAO == 0)
        {
            VAO = gl.GenVertexArray();
            gl.BindVertexArray(VAO);

            // Bind the shared mesh VAO’s EBO to this chunk VAO
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, sharedEBO);

            // --- Geometry buffer (Attribute 0) ---
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, cubeVBO);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);

            // --- Instance position buffer ---
            InstanceVBO = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVBO);
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
            gl.VertexAttribDivisor(1, 1);
            gl.EnableVertexAttribArray(1);

            // --- Instance data buffer ---
            DataVBO = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, DataVBO);
            gl.VertexAttribIPointer(2, 1, VertexAttribIType.UnsignedInt, sizeof(uint), (void*)0);
            gl.VertexAttribDivisor(2, 1);
            gl.EnableVertexAttribArray(2);
        }

        gl.BindVertexArray(VAO);

        // Upload positions
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

        // Upload voxel data
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
        int waterLevel = 28; // Doubled to match new resolution

        for (int x = 0; x < SizeX; x++)
        {
            for (int z = 0; z < SizeZ; z++)
            {
                double worldX = (ChunkX * SizeX) + x;
                double worldZ = (ChunkZ * SizeZ) + z;

                // Halved frequency (0.025) and doubled amplitude (60) to maintain world-scale shape
                double nHeight = noise.Noise(worldX * 0.025, worldZ * 0.025, 0.0);
                int terrainHeight = (int)((nHeight * 0.5 + 0.5) * 60) + 8;

                for (int y = 0; y < Height; y++)
                {
                    uint type = 0; // Default to Air

                    if (y < terrainHeight)
                    {
                        if (y == terrainHeight - 1)
                        {
                            // Use Sand (4u) if at or just above water level (14), otherwise Grass (1u)
                            type = (terrainHeight <= waterLevel + 1) ? 4u : 1u;
                        }
                        else type = 2u; // Dirt
                    }
                    else if (y < waterLevel)
                    {
                        type = 3u; // 3: Water
                    }
                    else break; // Optimization: stop loop if above terrain and water

                    byte growth = (byte)rand.Next(256);
                    byte moisture = (byte)rand.Next(256);

                    Voxels[x + SizeX * (y + Height * z)] = new Voxel(type, growth, moisture);
                }
            }
        }
        IsDirty = true;
    }
}