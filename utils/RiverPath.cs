using System.Collections.Generic;
using System.Numerics;

namespace VoxEngine.Utils;

public sealed class RiverPath
{
    public IReadOnlyList<Vector2> Points { get; }
    public bool IsTrunk { get; }
    public float StartChannelWidth { get; }
    public float EndChannelWidth { get; }
    public float StartValleyWidth { get; }
    public float EndValleyWidth { get; }
    public float StartChannelDepth { get; }
    public float EndChannelDepth { get; }
    public float StartValleyDepth { get; }
    public float EndValleyDepth { get; }
    public float SourceWaterHeight { get; }
    public float OutletWaterHeight { get; }

    public RiverPath(
        IReadOnlyList<Vector2> points,
        bool isTrunk,
        float startChannelWidth,
        float endChannelWidth,
        float startValleyWidth,
        float endValleyWidth,
        float startChannelDepth,
        float endChannelDepth,
        float startValleyDepth,
        float endValleyDepth,
        float sourceWaterHeight,
        float outletWaterHeight)
    {
        Points = points;
        IsTrunk = isTrunk;
        StartChannelWidth = startChannelWidth;
        EndChannelWidth = endChannelWidth;
        StartValleyWidth = startValleyWidth;
        EndValleyWidth = endValleyWidth;
        StartChannelDepth = startChannelDepth;
        EndChannelDepth = endChannelDepth;
        StartValleyDepth = startValleyDepth;
        EndValleyDepth = endValleyDepth;
        SourceWaterHeight = sourceWaterHeight;
        OutletWaterHeight = outletWaterHeight;
    }

    public float SampleWaterHeight(float downstream01)
        => Lerp(SourceWaterHeight, OutletWaterHeight, downstream01);

    public float SampleChannelWidth(float downstream01)
        => Lerp(StartChannelWidth, EndChannelWidth, downstream01);

    public float SampleValleyWidth(float downstream01)
        => Lerp(StartValleyWidth, EndValleyWidth, downstream01);

    public float SampleChannelDepth(float downstream01)
        => Lerp(StartChannelDepth, EndChannelDepth, downstream01);

    public float SampleValleyDepth(float downstream01)
        => Lerp(StartValleyDepth, EndValleyDepth, downstream01);

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * t;
}
