using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxEngine.Utils;

public class World
{
    private readonly Dictionary<(int, int), Chunk> _loadedChunks = new();
    private readonly Perlin _noise;
    private readonly int _seed;

    public  float[] _instancePositions;
    public  uint[] _instanceData;
    public int RenderDistance = 6; // Chunks in each direction = (2n+1)^2 total

    public World(int seed)
    {
        _seed = seed;
        _noise = new Perlin(new DeterministicRandom(seed));

        var posList = new List<float>();
        var dataList = new List<uint>();
        _instancePositions = posList.ToArray();
        _instanceData = dataList.ToArray();
        foreach (var chunk in GetActiveChunks())
    {
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int y = 0; y < Chunk.Height; y++)
                {
                    Voxel voxel = chunk.Voxels[x + Chunk.SizeX * (y + Chunk.Height * z)];
                    if (voxel.Data == 0) continue; // Skip empty/air voxels

                    // Scale instance positions by 0.5 to pack them tighter
                    posList.Add((chunk.ChunkX * Chunk.SizeX + x) * 0.5f);
                    posList.Add(y * 0.5f);
                    posList.Add((chunk.ChunkZ * Chunk.SizeZ + z) * 0.5f);
                    dataList.Add(voxel.Data);
                    
                    Console.WriteLine($"Added voxel at ({chunk.ChunkX * Chunk.SizeX + x}, {y}, {chunk.ChunkZ * Chunk.SizeZ + z}) with type {voxel.Data}");
                }
            }
        }
    }
    }

    public bool Update(Vector3 playerPos)
    {
        int pCx = (int)Math.Floor(playerPos.X / Chunk.SizeX);
        int pCz = (int)Math.Floor(playerPos.Z / Chunk.SizeZ);
        bool changed = false;
        
        // Load chunks in range
        for (int x = -RenderDistance; x <= RenderDistance; x++)
        {
            for (int z = -RenderDistance; z <= RenderDistance; z++)
            {
                int cx = pCx + x;
                int cz = pCz + z;
                if (!_loadedChunks.ContainsKey((cx, cz)))
                {
                    _loadedChunks.Add((cx, cz), new Chunk(cx, cz, _seed, _noise));
                    changed = true;
                }
            }
        }

        // Optional: Unload chunks too far away
        var toUnload = new List<(int, int)>();
        foreach (var coord in _loadedChunks.Keys)
        {
            if (Math.Abs(coord.Item1 - pCx) > RenderDistance + 1 || 
                Math.Abs(coord.Item2 - pCz) > RenderDistance + 1)
            {
                toUnload.Add(coord);
                changed = true;
            }
        }
        foreach (var coord in toUnload) _loadedChunks.Remove(coord);
        return changed;
    }

    public IEnumerable<Chunk> GetActiveChunks() => _loadedChunks.Values;

    public bool IsSolid(Vector3 pos)
    {
        int cx = (int)Math.Floor(pos.X / 16); // Assuming Chunk.SizeX is 16
        int cz = (int)Math.Floor(pos.Z / 16); // Assuming Chunk.SizeZ is 16

        if (_loadedChunks.TryGetValue((cx, cz), out var chunk))
        {
            // Replace with your actual chunk block access logic
            // return chunk.GetBlockAt(pos).IsSolid;
            return false; 
        }

        // If chunk isn't loaded, treat as solid (to prevent falling through the void) 
        // or air depending on your preference.
        return false;
    }
}