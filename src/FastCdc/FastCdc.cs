using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FastCdc.Tests")]
namespace FastCdc;

/// <summary>
/// Splits binary data into variable-sized chunks using content-defined chunking (FastCDC).
/// </summary>
public static class FastCdc
{
    /// <summary>
    /// Splits in-memory data into chunks.
    /// </summary>
    /// <param name="source">The byte data to chunk.</param>
    /// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="eof">Whether this span represents the end of the input stream.</param>
    /// <returns>A <see cref="ChunkEnumerator"/> for iterating through chunks.</returns>
    /// <remarks>
    /// For file or network data, use <see cref="Chunk(Stream, FastCdcOptions?, int)"/> or <see cref="ChunkAsync(Stream, FastCdcOptions?, int, CancellationToken)"/>
    /// to avoid reading the entire payload into memory.
    /// </remarks>
    public static ChunkEnumerator Chunk(
        ReadOnlySpan<byte> source,
        FastCdcOptions? options = null,
        bool eof = true)
    {
        var opts = options ?? FastCdcOptions.Default16KB;
        return new ChunkEnumerator(source, opts, eof);
    }

    /// <summary>
    /// Reads chunks sequentially from a stream.
    /// </summary>
    /// <param name="source">The stream to read.</param>
    /// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="bufferSize">The internal read buffer size in bytes, or 0 to use twice the maximum chunk size.</param>
    /// <returns>A sequence of <see cref="Chunk"/> instances read from the stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is not readable.</exception>
    /// <remarks>
    /// For asynchronous stream reading, use <see cref="ChunkAsync(Stream, FastCdcOptions?, int, CancellationToken)"/>.
    /// </remarks>
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

                    if (chunkSize == 0) break;

                    yield return new Chunk(chunkHash, streamOffset + windowOffset, chunkSize);
                    windowOffset += chunkSize;
                }

                streamOffset += windowOffset;

                if (eof) break;

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
    /// Reads chunks asynchronously from a stream.
    /// </summary>
    /// <param name="source">The stream to read.</param>
    /// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="bufferSize">The internal read buffer size in bytes, or 0 to use twice the maximum chunk size.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous sequence of <see cref="Chunk"/> instances read from the stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is not readable.</exception>
    /// <remarks>
    /// When reading from a <see cref="PipeReader"/>, use <see cref="ChunkAsync(PipeReader, FastCdcOptions?, CancellationToken)"/>.
    /// </remarks>
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

                    if (chunkSize == 0) break;

                    yield return new Chunk(chunkHash, streamOffset + windowOffset, chunkSize);
                    windowOffset += chunkSize;
                }

                streamOffset += windowOffset;

                if (eof) break;

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
    /// Reads chunks asynchronously from a pipeline reader without intermediate buffer copies.
    /// </summary>
    /// <param name="reader">The pipeline reader to read from.</param>
    /// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous sequence of <see cref="Chunk"/> instances read from the pipeline reader.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="reader"/> is <see langword="null"/>.</exception>
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
                    if (buffer.IsSingleSegment)
                    {
                        var span = buffer.FirstSpan;
                        var (chunkHash, chunkSize) = Cut(span, in opts, span.Length, result.IsCompleted);

                        if (chunkSize == 0) break;

                        yield return new Chunk(chunkHash, streamOffset, chunkSize);
                        streamOffset += chunkSize;
                        buffer = buffer.Slice(chunkSize);
                        consumed = buffer.Start;
                    }
                    else
                    {
                        if (!result.IsCompleted && buffer.Length < opts.MaxSize)
                            break;

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

        unchecked
        {
            while (i < len1)
            {
                hash = (hash >> 1) + hashes[source[i]];
                i++;
                if ((hash & maskS) == 0)
                    return (hash, i);
            }

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
