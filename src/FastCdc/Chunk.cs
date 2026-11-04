namespace FastCdc;

/// <summary>
/// Represents a chunk identified by FastCDC.
/// </summary>
/// <param name="Hash">The rolling hash computed for this chunk.</param>
/// <param name="Offset">The zero-based byte offset where the chunk starts in the source.</param>
/// <param name="Length">The length of the chunk in bytes.</param>
public readonly record struct Chunk(uint Hash, long Offset, int Length);
