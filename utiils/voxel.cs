namespace VoxEngine.Utils;

// Structure to handle bit-packed voxel data (Type, Growth, Moisture)
// Layout: [Moisture (8 bits)] [Growth (8 bits)] [Type (16 bits)]
public struct Voxel
{
    public uint Data;

    public uint Type
    {
        get => Data & 0xFFFFu;
        set => Data = (Data & ~0xFFFFu) | (value & 0xFFFFu);
    }

    public uint Growth
    {
        get => (Data >> 16) & 0xFFu;
        set => Data = (Data & ~(0xFFu << 16)) | ((value & 0xFFu) << 16);
    }

    public uint Moisture
    {
        get => (Data >> 24) & 0xFFu;
        set => Data = (Data & ~(0xFFu << 24)) | ((value & 0xFFu) << 24);
    }

    public Voxel(uint type, uint growth = 0, uint moisture = 0) 
        => Data = (type & 0xFFFFu) | ((growth & 0xFFu) << 16) | ((moisture & 0xFFu) << 24);
}
