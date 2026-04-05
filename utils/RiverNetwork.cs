using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxEngine.Utils;

public readonly record struct RiverInfluence(
    float ChannelMask,
    float ValleyMask,
    float WaterSurfaceHeight,
    float ChannelDepth,
    float ValleyDepth,
    bool IsTrunk,
    float Downstream01);

public sealed class RiverNetwork
{
    public IReadOnlyList<RiverPath> Trunks { get; }
    public IReadOnlyList<RiverPath> Tributaries { get; }

    public RiverNetwork(IReadOnlyList<RiverPath> trunks, IReadOnlyList<RiverPath> tributaries)
    {
        Trunks = trunks;
        Tributaries = tributaries;
    }

    public RiverInfluence QueryInfluence(Vector2 point)
    {
        RiverInfluence best = default;
        float bestScore = 0f;

        foreach (RiverPath path in Trunks)
        {
            SamplePathInfluence(path, point, ref best, ref bestScore);
        }

        foreach (RiverPath path in Tributaries)
        {
            SamplePathInfluence(path, point, ref best, ref bestScore);
        }

        return best;
    }

    private static void SamplePathInfluence(RiverPath path, Vector2 point, ref RiverInfluence best, ref float bestScore)
    {
        IReadOnlyList<Vector2> points = path.Points;
        if (points.Count < 2)
            return;

        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1];
            Vector2 b = points[i];
            Vector2 segment = b - a;
            float segmentLengthSquared = segment.LengthSquared();
            if (segmentLengthSquared < 0.0001f)
                continue;

            float t = Math.Clamp(Vector2.Dot(point - a, segment) / segmentLengthSquared, 0f, 1f);
            Vector2 closest = a + segment * t;
            float distance = Vector2.Distance(point, closest);

            float downstreamStart = (i - 1) / (float)(points.Count - 1);
            float downstreamEnd = i / (float)(points.Count - 1);
            float downstream01 = Lerp(downstreamStart, downstreamEnd, t);

            float channelWidth = path.SampleChannelWidth(downstream01);
            float valleyWidth = path.SampleValleyWidth(downstream01);
            float channelMask = 1f - SmoothStep(channelWidth * 0.55f, channelWidth, distance);
            float valleyMask = 1f - SmoothStep(valleyWidth * 0.60f, valleyWidth, distance);
            float score = MathF.Max(channelMask, valleyMask * 0.7f);

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = new RiverInfluence(
                channelMask,
                valleyMask,
                path.SampleWaterHeight(downstream01),
                path.SampleChannelDepth(downstream01),
                path.SampleValleyDepth(downstream01),
                path.IsTrunk,
                downstream01);
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Math.Abs(edge1 - edge0) < float.Epsilon)
            return value < edge0 ? 0f : 1f;

        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * t;
}
