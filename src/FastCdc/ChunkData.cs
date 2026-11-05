namespace FastCdc;

/// <summary>
/// Represents a chunk identified by FastCDC along with its underlying byte payload.
/// </summary>
/// <param name="Offset">The zero-based byte offset where the chunk starts in the source.</param>
/// <param name="Hash">The 64-bit rolling Gear hash computed for this chunk.</param>
/// <param name="Length">The length of the chunk in bytes.</param>
/// <param name="Memory">
/// The memory region containing the chunk payload. When produced by streaming overloads
/// (<see cref="FastCdc.ChunkData(Stream, FastCdcOptions?, int)"/> or <see cref="FastCdc.ChunkDataAsync(Stream, FastCdcOptions?, int, CancellationToken)"/>),
/// this memory is backed by an internal pooled buffer and is only valid during the current enumeration step.
/// Call <see cref="ToArray"/> if the data must be retained after advancing the enumerator.
/// </param>
public readonly record struct ChunkData(long Offset, ulong Hash, int Length, ReadOnlyMemory<byte> Memory)
{
	/// <summary>
	/// Gets a read-only span over the chunk payload bytes.
	/// </summary>
	public ReadOnlySpan<byte> Span => Memory.Span;

	/// <summary>
	/// Copies the chunk payload bytes into a new heap-allocated array.
	/// </summary>
	/// <returns>A new byte array containing the chunk payload.</returns>
	public byte[] ToArray() => Memory.ToArray();

	/// <summary>
	/// Converts a <see cref="ChunkData"/> instance into its metadata-only <see cref="Chunk"/> representation.
	/// </summary>
	/// <param name="data">The chunk data instance.</param>
	public static implicit operator Chunk(ChunkData data) => new(data.Offset, data.Hash, data.Length);
}

