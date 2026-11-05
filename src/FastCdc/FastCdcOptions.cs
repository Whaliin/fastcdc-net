using System.Numerics;

namespace FastCdc;

/// <summary>
/// Configuration options for FastCDC chunking.
/// </summary>
public readonly record struct FastCdcOptions
{
	/// <summary>Smallest acceptable value for the minimum chunk size (64 B).</summary>
	public const int MinimumMin = 64;

	/// <summary>Largest acceptable value for the minimum chunk size for V2020 and V2016 (1 MB).</summary>
	public const int MinimumMax = 1_048_576;

	/// <summary>Largest acceptable value for the minimum chunk size for Ronomon (64 MB).</summary>
	public const int RonomonMinimumMax = 67_108_864;

	/// <summary>Smallest acceptable value for the average chunk size (256 B).</summary>
	public const int AverageMin = 256;

	/// <summary>Largest acceptable value for the average chunk size for V2020 and V2016 (4 MB).</summary>
	public const int AverageMax = 4_194_304;

	/// <summary>Largest acceptable value for the average chunk size for Ronomon (256 MB).</summary>
	public const int RonomonAverageMax = 268_435_456;

	/// <summary>Smallest acceptable value for the maximum chunk size (1 KB).</summary>
	public const int MaximumMin = 1024;

	/// <summary>Largest acceptable value for the maximum chunk size for V2020 and V2016 (16 MB).</summary>
	public const int MaximumMax = 16_777_216;

	/// <summary>Largest acceptable value for the maximum chunk size for Ronomon (1 GB).</summary>
	public const int RonomonMaximumMax = 1_073_741_824;

	/// <summary>Default preset for ~16 KB average chunks (8 KB min, 16 KB avg, 32 KB max, V2020, Level1).</summary>
	public static FastCdcOptions Default16KB { get; } = new(8192, 16384, 32768);

	/// <summary>Default preset for ~32 KB average chunks (16 KB min, 32 KB avg, 64 KB max, V2020, Level1).</summary>
	public static FastCdcOptions Default32KB { get; } = new(16384, 32768, 65536);

	/// <summary>Default preset for ~64 KB average chunks (32 KB min, 64 KB avg, 128 KB max, V2020, Level1).</summary>
	public static FastCdcOptions Default64KB { get; } = new(32768, 65536, 131_072);

	/// <summary>Minimum allowed size for any generated chunk.</summary>
	public int MinSize { get; }

	/// <summary>Target average size for generated chunks.</summary>
	public int AvgSize { get; }

	/// <summary>Maximum allowed size for any generated chunk.</summary>
	public int MaxSize { get; }

	/// <summary>The FastCDC algorithm format used for chunking.</summary>
	public FastCdcFormat Format { get; }

	/// <summary>The chunk size normalization level.</summary>
	public NormalizationLevel Normalization { get; }

	/// <summary>Mask used for boundaries smaller than average (Phase 1).</summary>
	internal ulong MaskS { get; }

	/// <summary>Mask used for boundaries larger than average (Phase 2).</summary>
	internal ulong MaskL { get; }

	/// <summary>Left-shifted MaskS (MaskS &lt;&lt; 1) used for the first byte of each pair in V2020.</summary>
	internal ulong MaskSLs { get; }

	/// <summary>Left-shifted MaskL (MaskL &lt;&lt; 1) used for the first byte of each pair in V2020.</summary>
	internal ulong MaskLLs { get; }

	/// <summary>Offset relative to chunk start where Phase 1 ends.</summary>
	internal int CenterOffset { get; }

	/// <summary>
	/// Initializes a new <see cref="FastCdcOptions"/> instance with the specified chunk size bounds, format, and normalization level.
	/// </summary>
	/// <param name="minSize">Minimum chunk size in bytes.</param>
	/// <param name="avgSize">Average chunk size in bytes.</param>
	/// <param name="maxSize">Maximum chunk size in bytes.</param>
	/// <param name="format">The FastCDC algorithm format to use (defaults to <see cref="FastCdcFormat.V2020"/>).</param>
	/// <param name="normalization">The chunk size normalization level (defaults to <see cref="NormalizationLevel.Level1"/>).</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if any size, format, or normalization argument is outside its allowed range.</exception>
	/// <exception cref="ArgumentException">Thrown if size bounds are inverted, or if odd size bounds are supplied for <see cref="FastCdcFormat.V2020"/>.</exception>
	public FastCdcOptions(
		int minSize,
		int avgSize,
		int maxSize,
		FastCdcFormat format = FastCdcFormat.V2020,
		NormalizationLevel normalization = NormalizationLevel.Level1)
	{
		if (format is < FastCdcFormat.V2020 or > FastCdcFormat.Ronomon)
			throw new ArgumentOutOfRangeException(nameof(format), format, "Invalid FastCDC format.");
		if (normalization is < NormalizationLevel.Level0 or > NormalizationLevel.Level3)
			throw new ArgumentOutOfRangeException(nameof(normalization), normalization, "Invalid normalization level.");

		int minMax = format == FastCdcFormat.Ronomon ? RonomonMinimumMax : MinimumMax;
		int avgMax = format == FastCdcFormat.Ronomon ? RonomonAverageMax : AverageMax;
		int maxMax = format == FastCdcFormat.Ronomon ? RonomonMaximumMax : MaximumMax;

		if (minSize < MinimumMin || minSize > minMax)
			throw new ArgumentOutOfRangeException(nameof(minSize), minSize, $"Minimum chunk size must be between {MinimumMin} and {minMax}.");
		if (avgSize < AverageMin || avgSize > avgMax)
			throw new ArgumentOutOfRangeException(nameof(avgSize), avgSize, $"Average chunk size must be between {AverageMin} and {avgMax}.");
		if (maxSize < MaximumMin || maxSize > maxMax)
			throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, $"Maximum chunk size must be between {MaximumMin} and {maxMax}.");
		if (minSize > avgSize)
			throw new ArgumentException("Minimum chunk size must not be greater than average chunk size.", nameof(minSize));
		if (avgSize > maxSize)
			throw new ArgumentException("Average chunk size must not be greater than maximum chunk size.", nameof(avgSize));

		if (format == FastCdcFormat.V2020)
		{
			if ((minSize & 1) != 0)
				throw new ArgumentException("Minimum chunk size must be even when using FastCdcFormat.V2020.", nameof(minSize));
			if ((avgSize & 1) != 0)
				throw new ArgumentException("Average chunk size must be even when using FastCdcFormat.V2020.", nameof(avgSize));
			if ((maxSize & 1) != 0)
				throw new ArgumentException("Maximum chunk size must be even when using FastCdcFormat.V2020.", nameof(maxSize));
		}

		MinSize = minSize;
		AvgSize = avgSize;
		MaxSize = maxSize;
		Format = format;
		Normalization = normalization;

		uint bits = RoundLog2((uint)avgSize);
		uint norm = (uint)normalization;

		if (format == FastCdcFormat.Ronomon)
		{
			MaskS = Mask(bits + norm);
			MaskL = Mask(bits - norm);
			MaskSLs = 0UL;
			MaskLLs = 0UL;

			int offset = minSize + CeilDiv(minSize, 2);
			if (offset > avgSize)
				offset = avgSize;
			CenterOffset = avgSize - offset;
		}
		else
		{
			ReadOnlySpan<ulong> masks = Table.Masks;
			ulong maskS = masks[(int)(bits + norm)];
			ulong maskL = masks[(int)(bits - norm)];

			MaskS = maskS;
			MaskL = maskL;
			MaskSLs = maskS << 1;
			MaskLLs = maskL << 1;
			CenterOffset = avgSize;
		}
	}

	internal static uint RoundLog2(uint value)
	{
		int n = BitOperations.Log2(value);
		bool roundUp = (ulong)value * value > (1UL << (2 * n + 1));
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
