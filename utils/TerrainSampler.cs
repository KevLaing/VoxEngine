using System;
using System.Numerics;

namespace VoxEngine.Utils;

public sealed class TerrainSampler
{
    public const int WaterLevel = 28;

    private const float SpawnAnchorX = 30f;
    private const float SpawnAnchorZ = 30f;

    private const float TemperatureFrequency = 0.0010f;
    private const float MoistureFrequency = 0.0011f;
    private const float BroadTerrainFrequency = 0.0018f;
    private const float RollingTerrainFrequency = 0.0040f;
    private const float DetailFrequency = 0.0100f;
    private const float MountainRidgeFrequency = 0.0100f;

    private const float BaseTerrainHeight = 39f;
    private const float BroadTerrainAmplitude = 4.0f;
    private const float RollingTerrainAmplitude = 3.0f;
    private const float DetailAmplitude = 1.5f;

    private const float CoastNearestOffsetSouth = 180f;
    private const float CoastArcRadius = 1500f;
    private const float CoastShelfDepth = 9f;
    private const float OffshoreDepth = 18f;
    private const float BeachLift = 2.5f;

    private const float MountainBandInner = 80f;
    private const float MountainBandOuter = 180f;
    private const float MountainBaseHeight = 30f;
    private const float MountainRidgeHeight = 24f;
    private const float PeakAngularHalfWidthRadians = 0.18f;
    private const float PeakHeightMultiplier = 5.0f;
    private const float PeakCliffStrength = 72f;

    private readonly Perlin _temperatureNoise;
    private readonly Perlin _moistureNoise;
    private readonly Perlin _broadTerrainNoise;
    private readonly Perlin _rollingTerrainNoise;
    private readonly Perlin _detailNoise;
    private readonly Perlin _mountainRidgeNoise;

    private readonly float _mountainRingRadius;
    private readonly float _peakAngleRadians;

    public TerrainSampler(int seed)
    {
        _temperatureNoise = CreatePerlin(seed, 101);
        _moistureNoise = CreatePerlin(seed, 211);
        _broadTerrainNoise = CreatePerlin(seed, 307);
        _rollingTerrainNoise = CreatePerlin(seed, 401);
        _detailNoise = CreatePerlin(seed, 503);
        _mountainRidgeNoise = CreatePerlin(seed, 601);

        DeterministicRandom random = new(seed ^ 0x51A73C9);
        _mountainRingRadius = 1000f + (float)(random.NextDouble() * 200.0);
        _peakAngleRadians = DegreesToRadians(-30f + (float)(random.NextDouble() * 60.0));
    }

    public TerrainSample SampleColumn(int worldX, int worldZ)
    {
        float x = worldX;
        float z = worldZ;

        float temperature = RemapTo01(_temperatureNoise.Noise(x * TemperatureFrequency, z * TemperatureFrequency, 17.0));
        float baseMoisture = RemapTo01(_moistureNoise.Noise(x * MoistureFrequency, z * MoistureFrequency, 41.0));

        float broadTerrain = SampleSigned(_broadTerrainNoise, x, z, BroadTerrainFrequency);
        float rollingTerrain = SampleSigned(_rollingTerrainNoise, x, z, RollingTerrainFrequency);
        float detail = SampleSigned(_detailNoise, x, z, DetailFrequency);

        float dxFromSpawn = x - SpawnAnchorX;
        float coastlineZ = SpawnAnchorZ + CoastNearestOffsetSouth + (dxFromSpawn * dxFromSpawn) / (2f * CoastArcRadius);
        float signedCoastDistance = coastlineZ - z;

        float coastalShelfMask = 1f - SmoothStep(20f, 120f, signedCoastDistance);
        float offshoreMask = 1f - SmoothStep(-70f, 18f, signedCoastDistance);
        float beachMask =
            SmoothStep(-6f, 8f, signedCoastDistance) *
            (1f - SmoothStep(18f, 40f, signedCoastDistance));

        float northForestBias = SmoothStep(SpawnAnchorZ + 30f, SpawnAnchorZ - 220f, z);
        float moisture = Math.Clamp(baseMoisture + northForestBias * 0.22f - coastalShelfMask * 0.08f, 0f, 1f);

        float baseTerrain = BaseTerrainHeight
            + broadTerrain * BroadTerrainAmplitude
            + rollingTerrain * RollingTerrainAmplitude;

        float detailContribution = detail * DetailAmplitude;
        float coastalAdjustment = -coastalShelfMask * CoastShelfDepth - offshoreMask * OffshoreDepth + beachMask * BeachLift;

        float distanceFromSpawn = Vector2.Distance(new Vector2(x, z), new Vector2(SpawnAnchorX, SpawnAnchorZ));
        float ringDelta = MathF.Abs(distanceFromSpawn - _mountainRingRadius);
        float mountainMask = 1f - SmoothStep(MountainBandInner, MountainBandOuter, ringDelta);
        float mountainDistance01 = Math.Clamp(ringDelta / MountainBandOuter, 0f, 1f);
        float bearingFromNorth = MathF.Atan2(x - SpawnAnchorX, -(z - SpawnAnchorZ));
        float peakAngularDelta = MathF.Abs(WrapAngleRadians(bearingFromNorth - _peakAngleRadians));
        float peakDirectionalMask = 1f - SmoothStep(PeakAngularHalfWidthRadians * 0.45f, PeakAngularHalfWidthRadians, peakAngularDelta);
        float peakMask = mountainMask * peakDirectionalMask;

        float ridgeNoise = 0.6f + RemapTo01(_mountainRidgeNoise.Noise(x * MountainRidgeFrequency, z * MountainRidgeFrequency, 73.0)) * 0.8f;
        float rangeContribution = mountainMask * (MountainBaseHeight + ridgeNoise * MountainRidgeHeight);
        float peakMass = peakMask * rangeContribution * (PeakHeightMultiplier - 1f);
        float peakCliffs = peakMask * MathF.Pow(ridgeNoise, 2.4f) * PeakCliffStrength;
        float mountainContribution = rangeContribution + peakMass + peakCliffs;

        float surfaceHeight = baseTerrain + detailContribution + coastalAdjustment + mountainContribution;
        int finalHeight = Math.Clamp((int)MathF.Round(surfaceHeight), WaterLevel - 10, Chunk.Height - 2);

        BiomeType biome = SelectBiome(temperature, moisture, finalHeight, beachMask, coastalShelfMask, mountainMask, peakMask, northForestBias);

        return new TerrainSample(
            temperature,
            moisture,
            mountainDistance01,
            mountainMask,
            baseTerrain + coastalAdjustment + mountainContribution,
            detailContribution,
            coastalShelfMask,
            finalHeight,
            biome);
    }

    private static BiomeType SelectBiome(
        float temperature,
        float moisture,
        int surfaceHeight,
        float beachMask,
        float coastalShelfMask,
        float mountainMask,
        float peakMask,
        float northForestBias)
    {
        if (surfaceHeight <= WaterLevel + 1 && coastalShelfMask > 0.35f)
            return BiomeType.Fjord;

        if (peakMask > 0.22f || surfaceHeight > WaterLevel + 78)
            return BiomeType.Mountain;

        if (mountainMask > 0.65f)
            return BiomeType.Mountain;

        if (mountainMask > 0.35f)
            return BiomeType.RockyFoothills;

        if (beachMask > 0.30f || (coastalShelfMask > 0.55f && surfaceHeight <= WaterLevel + 4))
            return BiomeType.Plains;

        float forestScore =
            moisture * 1.30f +
            northForestBias * 0.65f +
            (1f - MathF.Abs(temperature - 0.55f)) * 0.25f;

        float plainsScore =
            (1f - MathF.Abs(moisture - 0.40f)) * 0.90f +
            coastalShelfMask * 0.30f +
            (1f - MathF.Abs(temperature - 0.58f)) * 0.30f;

        return forestScore >= plainsScore ? BiomeType.Forest : BiomeType.Plains;
    }

    private static Perlin CreatePerlin(int seed, int salt)
        => new(new DeterministicRandom(seed ^ (salt * 73856093)));

    private static float SampleSigned(Perlin perlin, float x, float z, float frequency, double layer = 0.0)
        => (float)perlin.Noise(x * frequency, z * frequency, layer);

    private static float RemapTo01(double value)
        => (float)(value * 0.5 + 0.5);

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Math.Abs(edge1 - edge0) < float.Epsilon)
            return value < edge0 ? 0f : 1f;

        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float DegreesToRadians(float degrees)
        => degrees * (MathF.PI / 180f);

    private static float WrapAngleRadians(float angle)
    {
        while (angle > MathF.PI)
            angle -= MathF.PI * 2f;

        while (angle < -MathF.PI)
            angle += MathF.PI * 2f;

        return angle;
    }
}
