// ============================================================================
// FastCdc.cs
//
// Public API entry points for content-defined chunking (FastCDC).
// Provides synchronous and asynchronous chunking over in-memory buffers (Span/Memory),
// streams (Stream), and pipeline readers (PipeReader).
//
// For the underlying boundary-detection and rolling hash algorithms,
// see FastCdc.Engine.cs.
// ============================================================================

using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FastCdc.Tests")]
namespace FastCdc;

/// <summary>
/// Splits binary data into variable-sized chunks using content-defined chunking (FastCDC).
/// </summary>
public static partial class FastCdc
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
		FastCdcOptions opts = ResolveOptions(options);
		return new ChunkEnumerator(source, opts, eof);
	}

	/// <summary>
	/// Splits in-memory data into chunks along with their underlying memory slices.
	/// </summary>
	/// <param name="source">The memory buffer to chunk.</param>
	/// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
	/// <param name="eof">Whether this buffer represents the end of the input stream.</param>
	/// <returns>A <see cref="ChunkDataEnumerator"/> for iterating through chunks and their payloads.</returns>
	public static ChunkDataEnumerator ChunkData(
		ReadOnlyMemory<byte> source,
		FastCdcOptions? options = null,
		bool eof = true)
	{
		FastCdcOptions opts = ResolveOptions(options);
		return new ChunkDataEnumerator(source, opts, eof);
	}

	/// <summary>
	/// Reads chunks sequentially from a stream.
	/// </summary>
	/// <param name="source">The stream to read.</param>
	/// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
	/// <param name="bufferSize">The internal read buffer size in bytes, or 0 to use twice the maximum chunk size.</param>
	/// <returns>A sequence of <see cref="Chunk"/> instances read from the stream.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is not readable or <paramref name="options"/> is uninitialized.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="bufferSize"/> is negative.</exception>
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

		FastCdcOptions opts = ResolveOptions(options);
		int capacity = GetStreamBufferCapacity(in opts, bufferSize);
		return ChunkStreamCore(source, opts, capacity);
	}

	private static IEnumerable<Chunk> ChunkStreamCore(
		Stream source,
		FastCdcOptions opts,
		int capacity)
	{
		byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

		try
		{
			int buffered = 0;
			long streamOffset = 0L;

			while (true)
			{
				int read = source.Read(rentedBuffer.AsSpan(buffered, capacity - buffered));
				buffered += read;
				bool eof = read == 0;

				int windowOffset = 0;

				while (true)
				{
					int remaining = buffered - windowOffset;
					if (remaining == 0 || (!eof && remaining < opts.MaxSize))
						break;

					(ulong chunkHash, int chunkSize) = Cut(
						rentedBuffer.AsSpan(windowOffset, remaining),
						in opts,
						remaining,
						eof);

					if (chunkSize == 0) break;

					yield return new Chunk(streamOffset + windowOffset, chunkHash, chunkSize);
					windowOffset += chunkSize;
				}

				streamOffset += windowOffset;

				if (eof) break;

				int unconsumed = buffered - windowOffset;
				if (windowOffset > 0 && unconsumed > 0)
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
	/// Reads chunks and their byte payloads sequentially from a stream in a single pass.
	/// </summary>
	/// <param name="source">The stream to read.</param>
	/// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
	/// <param name="bufferSize">The internal read buffer size in bytes, or 0 to use twice the maximum chunk size.</param>
	/// <returns>A sequence of <see cref="ChunkData"/> instances read from the stream.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is not readable or <paramref name="options"/> is uninitialized.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="bufferSize"/> is negative.</exception>
	/// <remarks>
	/// The <see cref="ChunkData.Memory"/> buffer yielded by this method is backed by a pooled array and is
	/// valid only during the current enumeration step. Copy the contents via <see cref="ChunkData.ToArray"/>
	/// if the payload must outlive the current iteration.
	/// </remarks>
	public static IEnumerable<ChunkData> ChunkData(
		Stream source,
		FastCdcOptions? options = null,
		int bufferSize = 0)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (!source.CanRead)
			throw new ArgumentException("Stream must be readable.", nameof(source));

		FastCdcOptions opts = ResolveOptions(options);
		int capacity = GetStreamBufferCapacity(in opts, bufferSize);
		return ChunkDataStreamCore(source, opts, capacity);
	}

	private static IEnumerable<ChunkData> ChunkDataStreamCore(
		Stream source,
		FastCdcOptions opts,
		int capacity)
	{
		byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

		try
		{
			int buffered = 0;
			long streamOffset = 0L;

			while (true)
			{
				int read = source.Read(rentedBuffer.AsSpan(buffered, capacity - buffered));
				buffered += read;
				bool eof = read == 0;

				int windowOffset = 0;

				while (true)
				{
					int remaining = buffered - windowOffset;
					if (remaining == 0 || (!eof && remaining < opts.MaxSize))
						break;

					(ulong chunkHash, int chunkSize) = Cut(
						rentedBuffer.AsSpan(windowOffset, remaining),
						in opts,
						remaining,
						eof);

					if (chunkSize == 0) break;

					yield return new ChunkData(
						streamOffset + windowOffset,
						chunkHash,
						chunkSize,
						rentedBuffer.AsMemory(windowOffset, chunkSize));
					windowOffset += chunkSize;
				}

				streamOffset += windowOffset;

				if (eof) break;

				int unconsumed = buffered - windowOffset;
				if (windowOffset > 0 && unconsumed > 0)
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
	/// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is not readable or <paramref name="options"/> is uninitialized.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="bufferSize"/> is negative.</exception>
	/// <remarks>
	/// When reading from a <see cref="PipeReader"/>, use <see cref="ChunkAsync(PipeReader, FastCdcOptions?, CancellationToken)"/>.
	/// </remarks>
	public static IAsyncEnumerable<Chunk> ChunkAsync(
		Stream source,
		FastCdcOptions? options = null,
		int bufferSize = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (!source.CanRead)
			throw new ArgumentException("Stream must be readable.", nameof(source));

		FastCdcOptions opts = ResolveOptions(options);
		int capacity = GetStreamBufferCapacity(in opts, bufferSize);
		return ChunkStreamAsyncCore(source, opts, capacity, cancellationToken);
	}

	private static async IAsyncEnumerable<Chunk> ChunkStreamAsyncCore(
		Stream source,
		FastCdcOptions opts,
		int capacity,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

		try
		{
			int buffered = 0;
			long streamOffset = 0L;

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				int read = await source.ReadAsync(rentedBuffer.AsMemory(buffered, capacity - buffered), cancellationToken).ConfigureAwait(false);
				buffered += read;
				bool eof = read == 0;

				int windowOffset = 0;

				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();

					int remaining = buffered - windowOffset;
					if (remaining == 0 || (!eof && remaining < opts.MaxSize))
						break;

					(ulong chunkHash, int chunkSize) = Cut(
						rentedBuffer.AsSpan(windowOffset, remaining),
						in opts,
						remaining,
						eof);

					if (chunkSize == 0) break;

					yield return new Chunk(streamOffset + windowOffset, chunkHash, chunkSize);
					windowOffset += chunkSize;
				}

				streamOffset += windowOffset;

				if (eof) break;

				int unconsumed = buffered - windowOffset;
				if (windowOffset > 0 && unconsumed > 0)
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
	/// Reads chunks and their byte payloads asynchronously from a stream in a single pass.
	/// </summary>
	/// <param name="source">The stream to read.</param>
	/// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
	/// <param name="bufferSize">The internal read buffer size in bytes, or 0 to use twice the maximum chunk size.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An asynchronous sequence of <see cref="ChunkData"/> instances read from the stream.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is not readable or <paramref name="options"/> is uninitialized.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="bufferSize"/> is negative.</exception>
	/// <remarks>
	/// The <see cref="ChunkData.Memory"/> buffer yielded by this method is backed by a pooled array and is
	/// valid only during the current enumeration step. Copy the contents via <see cref="ChunkData.ToArray"/>
	/// if the payload must outlive the current iteration.
	/// </remarks>
	public static IAsyncEnumerable<ChunkData> ChunkDataAsync(
		Stream source,
		FastCdcOptions? options = null,
		int bufferSize = 0,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (!source.CanRead)
			throw new ArgumentException("Stream must be readable.", nameof(source));

		FastCdcOptions opts = ResolveOptions(options);
		int capacity = GetStreamBufferCapacity(in opts, bufferSize);
		return ChunkDataStreamAsyncCore(source, opts, capacity, cancellationToken);
	}

	private static async IAsyncEnumerable<ChunkData> ChunkDataStreamAsyncCore(
		Stream source,
		FastCdcOptions opts,
		int capacity,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

		try
		{
			int buffered = 0;
			long streamOffset = 0L;

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				int read = await source.ReadAsync(rentedBuffer.AsMemory(buffered, capacity - buffered), cancellationToken).ConfigureAwait(false);
				buffered += read;
				bool eof = read == 0;

				int windowOffset = 0;

				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();

					int remaining = buffered - windowOffset;
					if (remaining == 0 || (!eof && remaining < opts.MaxSize))
						break;

					(ulong chunkHash, int chunkSize) = Cut(
						rentedBuffer.AsSpan(windowOffset, remaining),
						in opts,
						remaining,
						eof);

					if (chunkSize == 0) break;

					yield return new ChunkData(
						streamOffset + windowOffset,
						chunkHash,
						chunkSize,
						rentedBuffer.AsMemory(windowOffset, chunkSize));
					windowOffset += chunkSize;
				}

				streamOffset += windowOffset;

				if (eof) break;

				int unconsumed = buffered - windowOffset;
				if (windowOffset > 0 && unconsumed > 0)
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
	/// <exception cref="ArgumentException">Thrown if <paramref name="options"/> is uninitialized.</exception>
	public static IAsyncEnumerable<Chunk> ChunkAsync(
		PipeReader reader,
		FastCdcOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(reader);
		FastCdcOptions opts = ResolveOptions(options);
		return ChunkPipeAsyncCore(reader, opts, cancellationToken);
	}

	private static async IAsyncEnumerable<Chunk> ChunkPipeAsyncCore(
		PipeReader reader,
		FastCdcOptions opts,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		long streamOffset = 0L;
		byte[]? scratchBuffer = null;

		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
				ReadOnlySequence<byte> buffer = result.Buffer;
				SequencePosition consumed = buffer.Start;
				SequencePosition examined = buffer.End;

				try
				{
					while (buffer.Length > 0)
					{
						cancellationToken.ThrowIfCancellationRequested();

						if (!result.IsCompleted && buffer.Length < opts.MaxSize)
							break;

						ulong chunkHash;
						int chunkSize;
						{
							ReadOnlySpan<byte> span;
							int scanLength;

							if (buffer.FirstSpan.Length >= opts.MaxSize)
							{
								span = buffer.FirstSpan;
								scanLength = opts.MaxSize;
							}
							else if (buffer.IsSingleSegment)
							{
								span = buffer.FirstSpan;
								scanLength = span.Length;
							}
							else
							{
								scratchBuffer ??= ArrayPool<byte>.Shared.Rent(opts.MaxSize);
								scanLength = (int)Math.Min(buffer.Length, opts.MaxSize);
								buffer.Slice(0, scanLength).CopyTo(scratchBuffer);
								span = scratchBuffer.AsSpan(0, scanLength);
							}

							(chunkHash, chunkSize) = Cut(span, in opts, scanLength, result.IsCompleted);
						}

						if (chunkSize == 0) break;

						yield return new Chunk(streamOffset, chunkHash, chunkSize);
						streamOffset += chunkSize;
						buffer = buffer.Slice(chunkSize);
						consumed = buffer.Start;
					}
				}
				finally
				{
					reader.AdvanceTo(consumed, examined);
				}

				if (result.IsCompleted) break;
			}
		}
		finally
		{
			if (scratchBuffer != null)
				ArrayPool<byte>.Shared.Return(scratchBuffer);
		}
	}

	/// <summary>
	/// Reads chunks and their byte payloads asynchronously from a pipeline reader in a single pass.
	/// </summary>
	/// <param name="reader">The pipeline reader to read from.</param>
	/// <param name="options">The chunking options, or <see langword="null"/> to use <see cref="FastCdcOptions.Default16KB"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An asynchronous sequence of <see cref="ChunkData"/> instances read from the pipeline reader.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="options"/> is uninitialized.</exception>
	/// <remarks>
	/// The <see cref="ChunkData.Memory"/> buffer yielded by this method is backed by the pipeline segment or a pooled
	/// scratch array and is valid only during the current enumeration step. Copy the contents via <see cref="ChunkData.ToArray"/>
	/// if the payload must outlive the current iteration.
	/// </remarks>
	public static IAsyncEnumerable<ChunkData> ChunkDataAsync(
		PipeReader reader,
		FastCdcOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(reader);
		FastCdcOptions opts = ResolveOptions(options);
		return ChunkDataPipeAsyncCore(reader, opts, cancellationToken);
	}

	private static async IAsyncEnumerable<ChunkData> ChunkDataPipeAsyncCore(
		PipeReader reader,
		FastCdcOptions opts,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		long streamOffset = 0L;
		byte[]? scratchBuffer = null;

		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
				ReadOnlySequence<byte> buffer = result.Buffer;
				SequencePosition consumed = buffer.Start;
				SequencePosition examined = buffer.End;

				try
				{
					while (buffer.Length > 0)
					{
						cancellationToken.ThrowIfCancellationRequested();

						if (!result.IsCompleted && buffer.Length < opts.MaxSize)
							break;

						ulong chunkHash;
						int chunkSize;
						ReadOnlyMemory<byte> chunkMemory;

						if (buffer.FirstSpan.Length >= opts.MaxSize)
						{
							ReadOnlyMemory<byte> first = buffer.First;
							(chunkHash, chunkSize) = Cut(first.Span, in opts, opts.MaxSize, result.IsCompleted);
							chunkMemory = first.Slice(0, chunkSize);
						}
						else if (buffer.IsSingleSegment)
						{
							ReadOnlyMemory<byte> first = buffer.First;
							(chunkHash, chunkSize) = Cut(first.Span, in opts, first.Length, result.IsCompleted);
							chunkMemory = first.Slice(0, chunkSize);
						}
						else
						{
							scratchBuffer ??= ArrayPool<byte>.Shared.Rent(opts.MaxSize);
							int scanLength = (int)Math.Min(buffer.Length, opts.MaxSize);
							buffer.Slice(0, scanLength).CopyTo(scratchBuffer);
							(chunkHash, chunkSize) = Cut(scratchBuffer.AsSpan(0, scanLength), in opts, scanLength, result.IsCompleted);
							chunkMemory = scratchBuffer.AsMemory(0, chunkSize);
						}

						if (chunkSize == 0) break;

						yield return new ChunkData(streamOffset, chunkHash, chunkSize, chunkMemory);
						streamOffset += chunkSize;
						buffer = buffer.Slice(chunkSize);
						consumed = buffer.Start;
					}
				}
				finally
				{
					reader.AdvanceTo(consumed, examined);
				}

				if (result.IsCompleted) break;
			}
		}
		finally
		{
			if (scratchBuffer != null)
				ArrayPool<byte>.Shared.Return(scratchBuffer);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static FastCdcOptions ResolveOptions(FastCdcOptions? options)
	{
		if (options is not { } opts)
			return FastCdcOptions.Default16KB;

		if (opts.MaxSize == 0)
			ThrowUninitializedOptions();

		return opts;
	}

	private static int GetStreamBufferCapacity(in FastCdcOptions opts, int bufferSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

		long readSize = bufferSize > 0 ? bufferSize : (long)opts.MaxSize * 2;
		long capacity = Math.Min(readSize + opts.MaxSize, Array.MaxLength);
		return (int)capacity;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowUninitializedOptions()
		=> throw new ArgumentException(
			"FastCdcOptions must be initialized via a constructor or preset; default(FastCdcOptions) is not valid.",
			"options");
}
