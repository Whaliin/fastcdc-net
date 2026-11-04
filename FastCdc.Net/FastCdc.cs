using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FastCdc.Net.Tests")]
namespace FastCdc.Net;

/// <summary>
/// Provides high-performance content-defined chunking using the FastCDC algorithm.
/// </summary>
public static class FastCdc
{
    /// <summary>
    /// Returns a zero-allocation <see cref="ChunkEnumerator"/> over the chunks in <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The byte data to chunk.</param>
    /// <param name="options">Chunking configuration options. Defaults to <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="eof">Whether this span represents the end of the input.</param>
    public static ChunkEnumerator Chunk(
        ReadOnlySpan<byte> source,
        FastCdcOptions? options = null,
        bool eof = true)
    {
        var opts = options ?? FastCdcOptions.Default16KB;
        return new ChunkEnumerator(source, opts, eof);
    }

    /// <summary>
    /// Lazily yields chunks from a synchronous <see cref="Stream"/> using pooled buffers.
    /// Does not load the entire stream into memory.
    /// </summary>
    /// <param name="source">A readable stream.</param>
    /// <param name="options">Chunking configuration options. Defaults to <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="bufferSize">Optional read buffer size in bytes. Defaults to 2x maximum chunk size.</param>
    public static IEnumerable<Chunk> Chunk(
        Stream source,
        FastCdcOptions? options = null,
        int bufferSize = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(source));

        var opts = options ?? FastCdcOptions.Default16KB;
        var readSize = bufferSize > 0 ? bufferSize : opts.MaxSize * 2;
        var capacity = checked(readSize + opts.MaxSize);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

        try
        {
            var buffered = 0;
            var streamOffset = 0L;

            while (true)
            {
                var read = source.Read(rentedBuffer.AsSpan(buffered, capacity - buffered));
                buffered += read;
                var eof = read == 0;

                var windowOffset = 0;

                while (true)
                {
                    var remaining = buffered - windowOffset;
                    if (remaining == 0) break;

                    var (chunkHash, chunkSize) = Cut(
                        rentedBuffer.AsSpan(windowOffset, remaining),
                        in opts,
                        remaining,
                        eof);

                    if (chunkSize == 0) break; // Need more data from the stream

                    yield return new Chunk(chunkHash, streamOffset + windowOffset, chunkSize);
                    windowOffset += chunkSize;
                }

                streamOffset += windowOffset;

                if (eof) break;

                // Slide unconsumed bytes to the front of the rented buffer
                var unconsumed = buffered - windowOffset;
                if (unconsumed > 0)
                    rentedBuffer.AsSpan(windowOffset, unconsumed).CopyTo(rentedBuffer);
                buffered = unconsumed;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Lazily yields chunks from an asynchronous <see cref="Stream"/> using pooled buffers.
    /// </summary>
    /// <param name="source">A readable stream.</param>
    /// <param name="options">Chunking configuration options. Defaults to <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="bufferSize">Optional read buffer size in bytes. Defaults to 2x maximum chunk size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<Chunk> ChunkAsync(
        Stream source,
        FastCdcOptions? options = null,
        int bufferSize = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(source));

        var opts = options ?? FastCdcOptions.Default16KB;
        var readSize = bufferSize > 0 ? bufferSize : opts.MaxSize * 2;
        var capacity = checked(readSize + opts.MaxSize);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

        try
        {
            var buffered = 0;
            var streamOffset = 0L;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await source.ReadAsync(rentedBuffer.AsMemory(buffered, capacity - buffered), cancellationToken).ConfigureAwait(false);
                buffered += read;
                var eof = read == 0;

                var windowOffset = 0;

                while (true)
                {
                    var remaining = buffered - windowOffset;
                    if (remaining == 0) break;

                    var (chunkHash, chunkSize) = Cut(
                        rentedBuffer.AsSpan(windowOffset, remaining),
                        in opts,
                        remaining,
                        eof);

                    if (chunkSize == 0) break; // Need more data from the stream

                    yield return new Chunk(chunkHash, streamOffset + windowOffset, chunkSize);
                    windowOffset += chunkSize;
                }

                streamOffset += windowOffset;

                if (eof) break;

                // Slide unconsumed bytes to the front of the rented buffer
                var unconsumed = buffered - windowOffset;
                if (unconsumed > 0)
                    rentedBuffer.AsSpan(windowOffset, unconsumed).CopyTo(rentedBuffer);
                buffered = unconsumed;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Lazily yields chunks from a <see cref="PipeReader"/> without intermediate buffer copies.
    /// </summary>
    /// <param name="reader">The pipeline reader.</param>
    /// <param name="options">Chunking configuration options. Defaults to <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<Chunk> ChunkAsync(
        PipeReader reader,
        FastCdcOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var opts = options ?? FastCdcOptions.Default16KB;
        var streamOffset = 0L;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            var consumed = buffer.Start;
            var examined = buffer.End;

            try
            {
                while (buffer.Length > 0)
                {
                    // If buffer is contiguous, chunk directly with zero copies
                    if (buffer.IsSingleSegment)
                    {
                        var span = buffer.FirstSpan;
                        var (chunkHash, chunkSize) = Cut(span, in opts, span.Length, result.IsCompleted);

                        if (chunkSize == 0) break; // Need more data

                        yield return new Chunk(chunkHash, streamOffset, chunkSize);
                        streamOffset += chunkSize;
                        buffer = buffer.Slice(chunkSize);
                        consumed = buffer.Start;
                    }
                    else
                    {
                        // Multi-segment: if we have less than maxSize and not completed, ask for more
                        if (!result.IsCompleted && buffer.Length < opts.MaxSize)
                            break;

                        // Linearize into a rented buffer for cutting across segments
                        var copyLength = (int)Math.Min(buffer.Length, opts.MaxSize);
                        var rented = ArrayPool<byte>.Shared.Rent(copyLength);
                        try
                        {
                            buffer.Slice(0, copyLength).CopyTo(rented);
                            var (chunkHash, chunkSize) = Cut(rented.AsSpan(0, copyLength), in opts, copyLength, result.IsCompleted);

                            if (chunkSize == 0) break;

                            yield return new Chunk(chunkHash, streamOffset, chunkSize);
                            streamOffset += chunkSize;
                            buffer = buffer.Slice(chunkSize);
                            consumed = buffer.Start;
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(rented);
                        }
                    }
                }
            }
            finally
            {
                reader.AdvanceTo(consumed, examined);
            }

            if (result.IsCompleted) break;
        }
    }

    // -------------------------------------------------------------------------
    // Core FastCDC algorithm
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (uint hash, int size) Cut(
        ReadOnlySpan<byte> source,
        in FastCdcOptions options,
        int sourceSize,
        bool eof)
    {
        var minSize = options.MinSize;
        var maxSize = options.MaxSize;

        if (sourceSize <= minSize)
            return eof ? (0u, sourceSize) : (0u, 0);

        if (sourceSize > maxSize)
            sourceSize = maxSize;

        var centerOffset = options.CenterOffset;
        var len1 = centerOffset > sourceSize ? sourceSize : centerOffset;
        var len2 = sourceSize;

        ReadOnlySpan<uint> hashes = Table.Hashes;
        var maskS = options.MaskS;
        var maskL = options.MaskL;

        var hash = 0u;
        var i = minSize;

        // Wrapped arithmetic to guarantee immunity to overflow in checked contexts
        unchecked
        {
            // Phase 1: stricter mask for smaller-than-average chunks
            while (i < len1)
            {
                hash = (hash >> 1) + hashes[source[i]];
                i++;
                if ((hash & maskS) == 0)
                    return (hash, i);
            }

            // Phase 2: looser mask for larger-than-average chunks
            while (i < len2)
            {
                hash = (hash >> 1) + hashes[source[i]];
                i++;
                if ((hash & maskL) == 0)
                    return (hash, i);
            }
        }

        return (!eof && sourceSize < maxSize) ? (hash, 0) : (hash, sourceSize);
    }
}
