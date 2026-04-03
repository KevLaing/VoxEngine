using Silk.NET.OpenGL;
using VoxEngine.Utils;

namespace VoxEngine;

public sealed class ChunkMeshingScheduler
{
    public int MeshBuildBudgetPerFrame { get; set; }

    public ChunkMeshingScheduler(int meshBuildBudgetPerFrame)
    {
        MeshBuildBudgetPerFrame = meshBuildBudgetPerFrame;
    }

    public void Process(GL gl, World world)
    {
        int remainingBudget = MeshBuildBudgetPerFrame;

        foreach (Chunk chunk in world.GetDirtyChunks())
        {
            if (remainingBudget <= 0)
                break;

            chunk.BuildMesh(gl, world);
            chunk.IsDirty = false;
            remainingBudget--;
        }
    }
}
