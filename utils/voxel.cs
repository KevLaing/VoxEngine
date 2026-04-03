namespace VoxEngine.Utils;

/// <summary>
/// Represents a bit-packed voxel.
/// Layout: [Moisture (8 bits)] [Growth (8 bits)] [Type (16 bits)]
/// </summary>
public struct Voxel
{
    public uint Data;

    /// <summary>
    /// The Voxel Type ID (0-65535). Occupies bits 0-15.
    /// </summary>
    public uint Type
    {
        get => Data & 0xFFFFu;
        set => Data = (Data & ~0xFFFFu) | (value & 0xFFFFu);
    }

    /// <summary>
    /// The Growth/Maturity level (0-255). Occupies bits 16-23.
    /// </summary>
    public uint Growth
    {
        get => (Data >> 16) & 0xFFu;
        set => Data = (Data & ~(0xFFu << 16)) | ((value & 0xFFu) << 16);
    }

    /// <summary>
    /// The Moisture level (0-255). Occupies bits 24-31.
    /// </summary>
    public uint Moisture
    {
        get => (Data >> 24) & 0xFFu;
        set => Data = (Data & ~(0xFFu << 24)) | ((value & 0xFFu) << 24);
    }

    public Voxel(uint type, uint growth = 0, uint moisture = 0) 
        => Data = (type & 0xFFFFu) | ((growth & 0xFFu) << 16) | ((moisture & 0xFFu) << 24);
}
