using System.Buffers;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FastCdc.Net.Tests")]
namespace FastCdc.Net;

/// <summary>
/// Implements the FastCDC content-defined chunking algorithm.
/// </summary>
public static class FastCdc
{
    /// Smallest acceptable value for the minimum chunk size.
    public const uint MinimumMin = 64;
    /// Largest acceptable value for the minimum chunk size.
    public const uint MinimumMax = 67_108_864;
    /// Smallest acceptable value for the average chunk size.
    public const uint AverageMin = 256;
    /// Largest acceptable value for the average chunk size.
    public const uint AverageMax = 268_435_456;
    /// Smallest acceptable value for the maximum chunk size.
    public const uint MaximumMin = 1024;
    /// Largest acceptable value for the maximum chunk size.
    public const uint MaximumMax = 1_073_741_824;

    /// <summary>
    /// Returns a <see cref="ChunkEnumerator"/> over the chunks in <paramref name="source"/>.
    /// </summary>
    public static ChunkEnumerator GetChunks(
        ReadOnlySpan<byte> source,
        uint minSize, uint avgSize, uint maxSize,
        bool eof = true)
    {
        Validate(source.Length, minSize, avgSize, maxSize);
        var (maskS, maskL) = ComputeMasks(avgSize);
        return new ChunkEnumerator(source, minSize, avgSize, maxSize, maskS, maskL, eof);
    }

    /// <summary>
    /// Yields chunks from a <see cref="Stream"/> as a synchronous enumerable without loading the whole stream into memory.
    /// </summary>
    public static IEnumerable<Chunk> GetChunks(
        Stream source,
        uint minSize, uint avgSize, uint maxSize,
        int bufferSize = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(source));
        Validate(null, minSize, avgSize, maxSize);

        var (maskS, maskL) = ComputeMasks(avgSize);
        var readSize = bufferSize > 0 ? bufferSize : (int)maxSize * 2;
        var capacity = checked(readSize + (int)maxSize);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

        try
        {
            var buffered = 0;
            var streamOffset = 0u;

            while (true)
            {
                var read = source.Read(rentedBuffer.AsSpan(buffered, capacity - buffered));
                buffered += read;
                var eof = read == 0;

                var windowOffset = 0u;

                while (true)
                {
                    var remaining = (uint)(buffered - windowOffset);
                    if (remaining == 0) break;

                    var (chunkHash, chunkSize) = Cut(
                        rentedBuffer.AsSpan((int)windowOffset, (int)remaining),
                        minSize, avgSize, maxSize, maskS, maskL,
                        remaining, eof);

                    if (chunkSize == 0) break; // need more data

                    yield return new Chunk(chunkHash, streamOffset + windowOffset, chunkSize);
                    windowOffset += chunkSize;
                }

                streamOffset += windowOffset;

                if (eof) break;

                // Slide unconsumed bytes to the front of the buffer
                var unconsumed = buffered - (int)windowOffset;
                if (unconsumed > 0)
                    rentedBuffer.AsSpan((int)windowOffset, unconsumed).CopyTo(rentedBuffer);
                buffered = unconsumed;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Yields chunks from a <see cref="Stream"/> as an asynchronous enumerable.
    /// </summary>
    public static async IAsyncEnumerable<Chunk> GetChunksAsync(
        Stream source,
        uint minSize, uint avgSize, uint maxSize,
        int bufferSize = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(source));
        Validate(null, minSize, avgSize, maxSize);

        var (maskS, maskL) = ComputeMasks(avgSize);
        var readSize = bufferSize > 0 ? bufferSize : (int)maxSize * 2;
        var capacity = checked(readSize + (int)maxSize);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

        try
        {
            var buffered = 0;
            var streamOffset = 0u;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await source.ReadAsync(rentedBuffer.AsMemory(buffered, capacity - buffered), cancellationToken).ConfigureAwait(false);
                buffered += read;
                var eof = read == 0;

                var windowOffset = 0u;

                while (true)
                {
                    var remaining = (uint)(buffered - windowOffset);
                    if (remaining == 0) break;

                    var (chunkHash, chunkSize) = Cut(
                        rentedBuffer.AsSpan((int)windowOffset, (int)remaining),
                        minSize, avgSize, maxSize, maskS, maskL,
                        remaining, eof);

                    if (chunkSize == 0) break; // need more data

                    yield return new Chunk(chunkHash, streamOffset + windowOffset, chunkSize);
                    windowOffset += chunkSize;
                }

                streamOffset += windowOffset;

                if (eof) break;

                // Slide unconsumed bytes to the front of the buffer
                var unconsumed = buffered - (int)windowOffset;
                if (unconsumed > 0)
                    rentedBuffer.AsSpan((int)windowOffset, unconsumed).CopyTo(rentedBuffer);
                buffered = unconsumed;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    // -------------------------------------------------------------------------
    // Core algorithm
    // -------------------------------------------------------------------------

    internal static (uint hash, uint size) Cut(
        ReadOnlySpan<byte> source,
        uint minSize, uint avgSize, uint maxSize,
        uint maskS, uint maskL,
        uint sourceSize, bool eof)
    {
        if (sourceSize <= minSize)
            return eof ? (0u, sourceSize) : (0u, 0u);

        sourceSize = Math.Min(sourceSize, maxSize);

        // In local (0-based) coordinates:
        //   Phase 1 window: [minSize, centerOffset)   — stricter mask
        //   Phase 2 window: [centerOffset, sourceSize) — looser mask
        var centerOffset = CenterSize(avgSize, minSize, sourceSize);
        var sourceLen2 = sourceSize;

        ReadOnlySpan<uint> hashes = Table.Hashes;
        var src = source; // local to help the JIT with alias analysis

        var hash = 0u;
        var i = (int)minSize;
        var len1 = (int)centerOffset;   // end of phase 1
        var len2 = (int)sourceLen2;     // end of phase 2

        // Phase 1: smaller mask (harder to satisfy → smaller chunks)
        while (i < len1)
        {
            hash = (hash >> 1) + hashes[src[i]];
            i++;
            if ((hash & maskS) == 0)
                return (hash, (uint)i);
        }

        // Phase 2: larger mask (easier to satisfy → allows bigger chunks)
        while (i < len2)
        {
            hash = (hash >> 1) + hashes[src[i]];
            i++;
            if ((hash & maskL) == 0)
                return (hash, (uint)i);
        }

        return (!eof && sourceSize < maxSize) ? (hash, 0u) : (hash, sourceSize);
    }

    // -------------------------------------------------------------------------
    // Helper math — integer-only, no floating point
    // -------------------------------------------------------------------------

    internal static uint CenterSize(uint average, uint minimum, uint sourceSize)
    {
        var offset = minimum + CeilDiv(minimum, 2);
        if (offset > average)
            offset = average;

        var size = average - offset;
        return size > sourceSize ? sourceSize : size;
    }

    internal static uint Logarithm2(uint value)
    {
        // Nearest-integer log2 (rounds half-up), matching the original
        // Math.Round(Math.Log(value, 2)) behaviour.
        // We use: round up when value^2 > 2^(2n+1), i.e. value > 2^n * sqrt(2).
        var n = System.Numerics.BitOperations.Log2(value);
        // 2^(2n+1) may exceed ulong for large n, but chunk sizes are well under 2^31
        var roundUp = (ulong)value * value > (1UL << (2 * n + 1));
        return roundUp ? (uint)(n + 1) : (uint)n;
    }

    internal static uint CeilDiv(uint x, uint y) =>
        (x + y - 1) / y;

    internal static uint Mask(uint bits)
    {
        if (bits is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(bits), bits, "Bits must be between 1 and 31.");

        return (1u << (int)bits) - 1u;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static (uint maskS, uint maskL) ComputeMasks(uint avgSize)
    {
        var bits = Logarithm2(avgSize);
        return (Mask(bits + 1), Mask(bits - 1));
    }

    private static void Validate(int? sourceLength, uint minSize, uint avgSize, uint maxSize)
    {
        if (sourceLength is 0)
            throw new ArgumentException("Source must not be empty.");
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
    }
}
