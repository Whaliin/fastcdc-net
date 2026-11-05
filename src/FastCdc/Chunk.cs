namespace FastCdc;

/// <summary>
/// Represents a chunk identified by FastCDC.
/// </summary>
/// <param name="Offset">The zero-based byte offset where the chunk starts in the source.</param>
/// <param name="Hash">The 64-bit rolling Gear hash computed for this chunk.</param>
/// <param name="Length">The length of the chunk in bytes.</param>
public readonly record struct Chunk(long Offset, ulong Hash, int Length);
