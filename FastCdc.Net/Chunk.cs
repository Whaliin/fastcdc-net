namespace FastCdc.Net;

/// <summary>
/// An immutable value describing a single content-defined chunk.
/// </summary>
public readonly record struct Chunk(uint Hash, uint Offset, uint Length);
