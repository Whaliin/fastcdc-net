namespace FastCdc.Net;

/// <summary>
/// An immutable value describing a single content-defined chunk.
/// </summary>
/// <param name="Hash">The rolling hash computed by FastCDC for this chunk.</param>
/// <param name="Offset">The 64-bit zero-based byte offset within the source.</param>
/// <param name="Length">The size of the chunk in bytes.</param>
public readonly record struct Chunk(uint Hash, long Offset, int Length);
