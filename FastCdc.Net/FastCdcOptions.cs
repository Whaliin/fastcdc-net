using System.Numerics;

namespace FastCdc.Net;

/// <summary>
/// Configuration options for FastCDC content-defined chunking.
/// </summary>
public readonly record struct FastCdcOptions
{
    /// <summary>Smallest acceptable value for the minimum chunk size (64 B).</summary>
    public const int MinimumMin = 64;

    /// <summary>Largest acceptable value for the minimum chunk size (64 MB).</summary>
    public const int MinimumMax = 67_108_864;

    /// <summary>Smallest acceptable value for the average chunk size (256 B).</summary>
    public const int AverageMin = 256;

    /// <summary>Largest acceptable value for the average chunk size (256 MB).</summary>
    public const int AverageMax = 268_435_456;

    /// <summary>Smallest acceptable value for the maximum chunk size (1 KB).</summary>
    public const int MaximumMin = 1024;

    /// <summary>Largest acceptable value for the maximum chunk size (1 GB).</summary>
    public const int MaximumMax = 1_073_741_824;

    /// <summary>Default preset for ~16 KB average chunks (8 KB min, 16 KB avg, 32 KB max).</summary>
    public static FastCdcOptions Default16KB { get; } = new(8192, 16384, 32768);

    /// <summary>Default preset for ~32 KB average chunks (16 KB min, 32 KB avg, 64 KB max).</summary>
    public static FastCdcOptions Default32KB { get; } = new(16384, 32768, 65536);

    /// <summary>Default preset for ~64 KB average chunks (32 KB min, 64 KB avg, 128 KB max).</summary>
    public static FastCdcOptions Default64KB { get; } = new(32768, 65536, 131_072);

    /// <summary>Minimum allowed size for any generated chunk.</summary>
    public int MinSize { get; }

    /// <summary>Target average size for generated chunks.</summary>
    public int AvgSize { get; }

    /// <summary>Maximum allowed size for any generated chunk.</summary>
    public int MaxSize { get; }

    /// <summary>Stricter mask used for finding boundaries smaller than average (Phase 1).</summary>
    internal uint MaskS { get; }

    /// <summary>Looser mask used for finding boundaries larger than average (Phase 2).</summary>
    internal uint MaskL { get; }

    /// <summary>Precalculated center offset relative to chunk start where Phase 1 ends.</summary>
    internal int CenterOffset { get; }

    /// <summary>
    /// Initializes and validates a new <see cref="FastCdcOptions"/> instance.
    /// Pre-computes masks and offsets for optimal chunking performance.
    /// </summary>
    /// <param name="minSize">Minimum chunk size in bytes (64 to 67,108,864).</param>
    /// <param name="avgSize">Average chunk size in bytes (256 to 268,435,456).</param>
    /// <param name="maxSize">Maximum chunk size in bytes (1,024 to 1,073,741,824).</param>
    public FastCdcOptions(int minSize, int avgSize, int maxSize)
    {
        if (minSize is < MinimumMin or > MinimumMax)
            throw new ArgumentOutOfRangeException(nameof(minSize), minSize, $"Minimum chunk size must be between {MinimumMin} and {MinimumMax}.");
        if (avgSize is < AverageMin or > AverageMax)
            throw new ArgumentOutOfRangeException(nameof(avgSize), avgSize, $"Average chunk size must be between {AverageMin} and {AverageMax}.");
        if (maxSize is < MaximumMin or > MaximumMax)
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, $"Maximum chunk size must be between {MaximumMin} and {MaximumMax}.");
        if (minSize > avgSize)
            throw new ArgumentException("Minimum chunk size must not be greater than average chunk size.", nameof(minSize));
        if (avgSize > maxSize)
            throw new ArgumentException("Average chunk size must not be greater than maximum chunk size.", nameof(avgSize));

        MinSize = minSize;
        AvgSize = avgSize;
        MaxSize = maxSize;

        var bits = RoundLog2((uint)avgSize);
        MaskS = Mask(bits + 1);
        MaskL = Mask(bits - 1);

        // Calculate center offset once:
        // offset = min + ceil(min / 2)
        // size = avg - min(offset, avg)
        var offset = minSize + CeilDiv(minSize, 2);
        if (offset > avgSize)
            offset = avgSize;
        CenterOffset = avgSize - offset;
    }

    internal static uint RoundLog2(uint value)
    {
        var n = BitOperations.Log2(value);
        var roundUp = (ulong)value * value > (1UL << (2 * n + 1));
        return roundUp ? (uint)(n + 1) : (uint)n;
    }

    internal static int CeilDiv(int x, int y) => (x + y - 1) / y;

    internal static uint Mask(uint bits)
    {
        if (bits is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(bits), bits, "Bits must be between 1 and 31.");
        return (1u << (int)bits) - 1u;
    }
}
