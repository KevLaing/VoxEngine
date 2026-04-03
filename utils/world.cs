using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace VoxEngine.Utils;

public class World
{
    private readonly Dictionary<(int, int), Chunk> _loadedChunks = new();
    private readonly Perlin _noise;
    private readonly int _seed;
    private static readonly (int X, int Z)[] CardinalNeighborOffsets =
    {
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1),
    };

    public float[] _instancePositions;
    public uint[] _instanceData;
    public int RenderDistance = 6; // Chunks in each direction = (2n+1)^2 total

    public World(int seed)
    {
        _seed = seed;
        _noise = new Perlin(new DeterministicRandom(seed));

        var posList = new List<float>();
        var dataList = new List<uint>();
        _instancePositions = posList.ToArray();
        _instanceData = dataList.ToArray();
    }
    public bool IsSolid(int worldX, int worldY, int worldZ)
    {
        if (worldY < 0 || worldY >= Chunk.Height)
            return false;

        int chunkX = (int)MathF.Floor((float)worldX / Chunk.SizeX);
        int chunkZ = (int)MathF.Floor((float)worldZ / Chunk.SizeZ);

        if (!_loadedChunks.TryGetValue((chunkX, chunkZ), out var chunk))
            return false;

        int localX = worldX - chunkX * Chunk.SizeX;
        int localZ = worldZ - chunkZ * Chunk.SizeZ;

        if (localX < 0 || localX >= Chunk.SizeX || localZ < 0 || localZ >= Chunk.SizeZ)
            return false;

        return chunk.Voxels[localX + Chunk.SizeX * (worldY + Chunk.Height * localZ)].Data != 0;
    }
    public bool Update(Vector3 playerPos, GL gl)
    {
        int pCx = (int)Math.Floor(playerPos.X / Chunk.SizeX);
        int pCz = (int)Math.Floor(playerPos.Z / Chunk.SizeZ);
        bool changed = false;

        for (int x = -RenderDistance; x <= RenderDistance; x++)
        {
            for (int z = -RenderDistance; z <= RenderDistance; z++)
            {
                int cx = pCx + x;
                int cz = pCz + z;

                if (!_loadedChunks.ContainsKey((cx, cz)))
                {
                    AddChunk(cx, cz);
                    changed = true;
                }
            }
        }

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

        foreach (var coord in toUnload)
        {
            RemoveChunk(coord.Item1, coord.Item2, gl);
        }

        return changed;
    }

    private void AddChunk(int cx, int cz)
    {
        var chunk = new Chunk(cx, cz, _seed, _noise)
        {
            IsDirty = true
        };

        _loadedChunks.Add((cx, cz), chunk);
        MarkCardinalNeighborsDirty(cx, cz);
    }

    private void RemoveChunk(int cx, int cz, GL gl)
    {
        if (!_loadedChunks.Remove((cx, cz), out Chunk? chunk))
            return;

        chunk.ReleaseMesh(gl);
        MarkCardinalNeighborsDirty(cx, cz);
    }

    private void MarkCardinalNeighborsDirty(int cx, int cz)
    {
        foreach (var offset in CardinalNeighborOffsets)
        {
            MarkNeighborDirty(cx + offset.X, cz + offset.Z);
        }
    }

    private void MarkNeighborDirty(int cx, int cz)
    {
        if (_loadedChunks.TryGetValue((cx, cz), out var chunk))
            chunk.IsDirty = true;
    }

    public IEnumerable<Chunk> GetActiveChunks() => _loadedChunks.Values;
    public IEnumerable<Chunk> GetDirtyChunks()
    {
        foreach (Chunk chunk in _loadedChunks.Values)
        {
            if (chunk.IsDirty)
                yield return chunk;
        }
    }

    public bool IsSolid(Vector3 pos)
    {
        int cx = (int)Math.Floor(pos.X / Chunk.SizeX);
        int cz = (int)Math.Floor(pos.Z / Chunk.SizeZ);

        if (_loadedChunks.TryGetValue((cx, cz), out var chunk))
        {
            int lx = (int)Math.Floor(pos.X) % Chunk.SizeX;
            int ly = (int)Math.Floor(pos.Y);
            int lz = (int)Math.Floor(pos.Z) % Chunk.SizeZ;

            if (lx < 0) lx += Chunk.SizeX;
            if (lz < 0) lz += Chunk.SizeZ;

            if (ly >= 0 && ly < Chunk.Height)
            {
                var voxel = chunk.Voxels[lx + Chunk.SizeX * (ly + Chunk.Height * lz)];
                return voxel.Data != 0; // Solid if not air
            }
            return false;
        }

        // If chunk isn't loaded, treat as solid (to prevent falling through the void) 
        // or air depending on your preference.
        return false;
    }
}
