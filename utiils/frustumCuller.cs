using System;
using System.Numerics;

namespace VoxEngine.Utils;

public static class FrustumCuller
{
    public static bool IntersectsAabb(Matrix4x4 viewProj, Vector3 min, Vector3 max)
    {
        Span<Vector3> corners = stackalloc Vector3[8]
        {
            new(min.X, min.Y, min.Z),
            new(max.X, min.Y, min.Z),
            new(min.X, max.Y, min.Z),
            new(max.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z),
            new(max.X, min.Y, max.Z),
            new(min.X, max.Y, max.Z),
            new(max.X, max.Y, max.Z),
        };

        bool allLeft = true;
        bool allRight = true;
        bool allBottom = true;
        bool allTop = true;
        bool allBehind = true;
        bool allBeyond = true;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector4 clip = Vector4.Transform(new Vector4(corners[i], 1f), viewProj);

            allLeft   &= clip.X < -clip.W;
            allRight  &= clip.X >  clip.W;
            allBottom &= clip.Y < -clip.W;
            allTop    &= clip.Y >  clip.W;
            allBehind &= clip.Z < -clip.W;
            allBeyond &= clip.Z >  clip.W;
        }

        return !(allLeft || allRight || allBottom || allTop || allBehind || allBeyond);
    }
}