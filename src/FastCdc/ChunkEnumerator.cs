namespace FastCdc;

/// <summary>
/// Enumerates chunks over a contiguous memory buffer.
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

    /// <summary>Returns this enumerator for foreach iteration.</summary>
    public readonly ChunkEnumerator GetEnumerator() => this;

    /// <summary>Gets the chunk at the current position of the enumerator.</summary>
    public readonly Chunk Current => _current;

    /// <summary>Advances the enumerator to the next chunk in the source.</summary>
    /// <returns><see langword="true"/> if the enumerator successfully advanced to the next chunk; <see langword="false"/> if the end of the source was reached.</returns>
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

        _current = new Chunk(_offset, hash, size);
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
