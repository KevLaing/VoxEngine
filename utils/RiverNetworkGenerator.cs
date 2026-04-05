using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxEngine.Utils;

public sealed class RiverNetworkGenerator
{
    private const int MajorRiverCount = 6;
    private const int TributaryCount = 4;
    private const float StepLength = 28f;
    private const float MaxTurnRadians = 0.85f;
    private const float ProgressWeight = 1.3f;
    private const float SmoothnessWeight = 0.55f;
    private const float DownhillWeight = 0.85f;
    private const float UphillPenalty = 1.75f;
    private const float LoopPenalty = 2.2f;
    private const float MeanderStrength = 0.38f;

    private readonly Func<Vector2, float> _terrainHeightSampler;
    private readonly Func<float, float> _coastlineSampler;
    private readonly Vector2 _spawn;
    private readonly float _mountainRingRadius;
    private readonly float _peakAngleRadians;
    private readonly DeterministicRandom _random;

    public RiverNetworkGenerator(
        Func<Vector2, float> terrainHeightSampler,
        Func<float, float> coastlineSampler,
        Vector2 spawn,
        float mountainRingRadius,
        float peakAngleRadians,
        int seed)
    {
        _terrainHeightSampler = terrainHeightSampler;
        _coastlineSampler = coastlineSampler;
        _spawn = spawn;
        _mountainRingRadius = mountainRingRadius;
        _peakAngleRadians = peakAngleRadians;
        _random = new DeterministicRandom(seed ^ 0x6F12B37);
    }

    public RiverNetwork Generate()
    {
        List<RiverPath> trunks = new();
        List<RiverPath> tributaries = new();

        for (int i = 0; i < MajorRiverCount; i++)
        {
            float trunkT = MajorRiverCount == 1 ? 0.5f : i / (float)(MajorRiverCount - 1);
            float sourceAngle = DegreesToRadians(Lerp(-110f, 110f, trunkT) + RandomRange(-7f, 7f));
            float outletX = _spawn.X + Lerp(-520f, 520f, trunkT) + RandomRange(-35f, 35f);

            Vector2 source = _spawn + DirectionFromNorth(sourceAngle) * (_mountainRingRadius - 250f);
            Vector2 outlet = new(outletX, _coastlineSampler(outletX) + RandomRange(4f, 14f));
            float sourceHeight = _terrainHeightSampler(source) - 2f;
            float outletHeight = TerrainSampler.WaterLevel + 1f;

            List<Vector2> points = BuildPath(source, outlet, sourceAngle, 1.0 + i * 0.37);
            trunks.Add(new RiverPath(
                points,
                true,
                10f,
                22f,
                40f,
                110f,
                3.5f,
                7.0f,
                1.5f,
                4.5f,
                sourceHeight,
                outletHeight));
        }

        for (int i = 0; i < TributaryCount; i++)
        {
            RiverPath parent = trunks[i % trunks.Count];
            int mergeIndex = (int)(Lerp(0.30f, 0.68f, (_random.NextDouble() > 0.5 ? (float)_random.NextDouble() : (float)_random.NextDouble())) * (parent.Points.Count - 1));
            mergeIndex = Math.Clamp(mergeIndex, 4, parent.Points.Count - 3);

            Vector2 mergePoint = parent.Points[mergeIndex];
            Vector2 parentForward = Vector2.Normalize(parent.Points[mergeIndex + 1] - parent.Points[mergeIndex - 1]);
            Vector2 lateral = new(-parentForward.Y, parentForward.X);
            if ((i & 1) == 1)
                lateral = -lateral;

            Vector2 fromSpawn = Vector2.Normalize(mergePoint - _spawn);
            Vector2 source = mergePoint + fromSpawn * RandomRange(140f, 230f) + lateral * RandomRange(90f, 170f);
            float sourceAngle = MathF.Atan2(source.X - _spawn.X, -(source.Y - _spawn.Y));
            float sourceHeight = _terrainHeightSampler(source) - 1.5f;
            float outletHeight = parent.SampleWaterHeight(mergeIndex / (float)(parent.Points.Count - 1));

            List<Vector2> points = BuildPath(source, mergePoint, sourceAngle, 4.0 + i * 0.41);
            tributaries.Add(new RiverPath(
                points,
                false,
                7f,
                10f,
                24f,
                48f,
                2.2f,
                3.5f,
                0.8f,
                1.6f,
                sourceHeight,
                outletHeight));
        }

        return new RiverNetwork(trunks, tributaries);
    }

    private List<Vector2> BuildPath(Vector2 source, Vector2 target, float sourceAngle, double phase)
    {
        List<Vector2> points = new() { source };
        Vector2 current = source;
        Vector2 heading = Vector2.Normalize(target - source);
        if (heading.LengthSquared() < 0.0001f)
            heading = DirectionFromNorth(sourceAngle);

        int maxSteps = Math.Max(32, (int)(Vector2.Distance(source, target) / StepLength * 2.4f));

        for (int step = 0; step < maxSteps; step++)
        {
            if (Vector2.Distance(current, target) <= StepLength * 1.6f)
            {
                points.Add(target);
                break;
            }

            Vector2 toTarget = Vector2.Normalize(target - current);
            float currentHeight = _terrainHeightSampler(current);
            float meanderBias = MathF.Sin((float)(phase + step * 0.47)) * MeanderStrength;

            float bestScore = float.NegativeInfinity;
            Vector2 bestCandidate = current + toTarget * StepLength;
            Vector2 bestHeading = toTarget;

            for (int i = -3; i <= 3; i++)
            {
                float angle = meanderBias + i * (MaxTurnRadians / 3f);
                Vector2 candidateHeading = Rotate(heading, angle);
                Vector2 candidate = current + candidateHeading * StepLength;
                float nextHeight = _terrainHeightSampler(candidate);
                float progress = Vector2.Distance(current, target) - Vector2.Distance(candidate, target);
                float downhill = currentHeight - nextHeight;
                float climb = MathF.Max(0f, nextHeight - currentHeight);
                float smoothness = Vector2.Dot(candidateHeading, heading);
                float loopPenalty = IsNearExisting(points, candidate) ? LoopPenalty : 0f;

                float score =
                    progress * ProgressWeight +
                    smoothness * SmoothnessWeight +
                    downhill * DownhillWeight -
                    climb * UphillPenalty -
                    loopPenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                    bestHeading = candidateHeading;
                }
            }

            current = bestCandidate;
            heading = Vector2.Normalize(bestHeading * 0.55f + toTarget * 0.45f);
            points.Add(current);
        }

        if (points[^1] != target)
            points.Add(target);

        return points;
    }

    private static bool IsNearExisting(List<Vector2> points, Vector2 candidate)
    {
        int count = points.Count;
        for (int i = 0; i < count - 4; i++)
        {
            if (Vector2.Distance(points[i], candidate) < StepLength * 0.9f)
                return true;
        }

        return false;
    }

    private float RandomRange(float min, float max)
        => min + (float)_random.NextDouble() * (max - min);

    private static float DegreesToRadians(float degrees)
        => degrees * (MathF.PI / 180f);

    private static Vector2 DirectionFromNorth(float angleRadians)
        => new(MathF.Sin(angleRadians), -MathF.Cos(angleRadians));

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos);
    }

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * t;
}
