using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxEngine.Utils;

public class World
{
    private readonly Dictionary<(int, int), Chunk> _loadedChunks = new();
    private readonly Perlin _noise;
    private readonly int _seed;
    public int RenderDistance = 4; // Chunks in each direction

    public World(int seed)
    {
        _seed = seed;
        _noise = new Perlin(new DeterministicRandom(seed));
    }

    public void Update(Vector3 playerPos)
    {
        int pCx = (int)Math.Floor(playerPos.X / Chunk.SizeX);
        int pCz = (int)Math.Floor(playerPos.Z / Chunk.SizeZ);

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
            }
        }
        foreach (var coord in toUnload) _loadedChunks.Remove(coord);
    }

    public IEnumerable<Chunk> GetActiveChunks() => _loadedChunks.Values;
}