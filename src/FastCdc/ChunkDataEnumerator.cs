namespace FastCdc;

/// <summary>
/// Enumerates chunks and their underlying memory slices over a <see cref="ReadOnlyMemory{T}"/> buffer.
/// </summary>
public struct ChunkDataEnumerator
{
	private readonly ReadOnlyMemory<byte> _source;
	private readonly FastCdcOptions _options;
	private readonly bool _eof;

	private int _offset;
	private ChunkData _current;

	internal ChunkDataEnumerator(ReadOnlyMemory<byte> source, FastCdcOptions options, bool eof)
	{
		_source = source;
		_options = options;
		_eof = eof;
		_offset = 0;
		_current = default;
	}

	/// <summary>Returns this enumerator for foreach iteration.</summary>
	public readonly ChunkDataEnumerator GetEnumerator() => this;

	/// <summary>Gets the chunk data at the current position of the enumerator.</summary>
	public readonly ChunkData Current => _current;

	/// <summary>Advances the enumerator to the next chunk in the source.</summary>
	/// <returns><see langword="true"/> if the enumerator successfully advanced to the next chunk; <see langword="false"/> if the end of the source was reached.</returns>
	public bool MoveNext()
	{
		int remaining = _source.Length - _offset;
		if (remaining == 0) return false;

		ReadOnlyMemory<byte> slice = _source.Slice(_offset, remaining);
		(ulong hash, int size) = FastCdc.Cut(
			slice.Span,
			in _options,
			remaining,
			_eof);

		if (size == 0) return false;

		_current = new ChunkData(_offset, hash, size, slice.Slice(0, size));
		_offset += size;
		return true;
	}

	/// <summary>Collects all remaining chunks into a <see cref="List{ChunkData}"/>.</summary>
	public List<ChunkData> ToList()
	{
		List<ChunkData> list = new();
		while (MoveNext())
			list.Add(_current);
		return list;
	}

	/// <summary>Collects all remaining chunks into an array.</summary>
	public ChunkData[] ToArray() => ToList().ToArray();
}

