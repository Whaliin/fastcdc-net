namespace FastCdc.Net;

/// <summary>
/// A stack-allocated, forward-only enumerator over FastCDC chunks.
/// Usable directly in a <c>foreach</c> loop with zero heap allocation.
/// Obtained via <see cref="FastCdc.Chunk(ReadOnlySpan{byte}, FastCdcOptions)"/>.
/// </summary>
public ref struct ChunkEnumerator
{
    private readonly ReadOnlySpan<byte> _source;
    private readonly FastCdcOptions _options;
    private readonly bool _eof;

    private int _offset;
    private Chunk _current;

    internal ChunkEnumerator(ReadOnlySpan<byte> source, FastCdcOptions options, bool eof)
    {
        _source = source;
        _options = options;
        _eof = eof;
        _offset = 0;
        _current = default;
    }

    /// <summary>Returns this enumerator (satisfies the foreach pattern).</summary>
    public readonly ChunkEnumerator GetEnumerator() => this;

    /// <summary>The current chunk.</summary>
    public readonly Chunk Current => _current;

    /// <summary>Advances to the next chunk. Returns <c>false</c> when exhausted.</summary>
    public bool MoveNext()
    {
        var remaining = _source.Length - _offset;
        if (remaining == 0) return false;

        var (hash, size) = FastCdc.Cut(
            _source.Slice(_offset, remaining),
            in _options,
            remaining,
            _eof);

        if (size == 0) return false;

        _current = new Chunk(hash, _offset, size);
        _offset += size;
        return true;
    }

    /// <summary>Collects all remaining chunks into a <see cref="List{Chunk}"/>.</summary>
    public List<Chunk> ToList()
    {
        var list = new List<Chunk>();
        while (MoveNext())
            list.Add(_current);
        return list;
    }

    /// <summary>Collects all remaining chunks into an array.</summary>
    public Chunk[] ToArray() => ToList().ToArray();
}
