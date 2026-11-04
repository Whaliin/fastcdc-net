namespace FastCdc.Net;

/// <summary>
/// A stack-allocated, forward-only enumerator over FastCDC chunks.
/// Usable directly in a <c>foreach</c> loop with zero heap allocation.
/// Obtained via <see cref="FastCdc.GetChunks"/>.
/// </summary>
public ref struct ChunkEnumerator
{
	private readonly ReadOnlySpan<byte> _source;
	private readonly uint _minSize;
	private readonly uint _avgSize;
	private readonly uint _maxSize;
	private readonly uint _maskS;
	private readonly uint _maskL;
	private readonly bool _eof;

	private uint _offset;
	private Chunk _current;

	internal ChunkEnumerator(
		ReadOnlySpan<byte> source,
		uint minSize, uint avgSize, uint maxSize,
		uint maskS, uint maskL,
		bool eof)
	{
		_source = source;
		_minSize = minSize;
		_avgSize = avgSize;
		_maxSize = maxSize;
		_maskS = maskS;
		_maskL = maskL;
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
		var remaining = (uint)(_source.Length - _offset);
		if (remaining == 0) return false;

		var (hash, size) = FastCdc.Cut(
			_source[(int)_offset..],
			_minSize, _avgSize, _maxSize,
			_maskS, _maskL,
			remaining, _eof);

		if (size == 0) return false;

		_current = new Chunk(hash, _offset, size);
		_offset += size;
		return true;
	}

	/// <summary>Collects all remaining chunks into a <see cref="List{T}"/>.</summary>
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
