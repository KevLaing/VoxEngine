using System;
using System.Collections.Generic;
using System.Numerics;
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
    }
}