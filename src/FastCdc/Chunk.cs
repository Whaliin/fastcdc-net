using System.Runtime.InteropServices;

namespace FastCdc;

/// <summary>
/// Represents a chunk identified by FastCDC.
/// </summary>
/// <param name="Offset">The zero-based byte offset where the chunk starts in the source.</param>
/// <param name="Hash">The rolling hash computed for this chunk.</param>
/// <param name="Length">The length of the chunk in bytes.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Chunk(long Offset, uint Hash, int Length);
