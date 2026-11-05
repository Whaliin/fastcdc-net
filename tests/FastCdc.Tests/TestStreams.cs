namespace FastCdc.Tests;

internal sealed class UnreadableStream : MemoryStream
{
	public override bool CanRead => false;
}

internal sealed class ChunkedReadStream(byte[] data, int maxChunkSize) : Stream
{
	private int _position;
	public override bool CanRead => true;
	public override bool CanSeek => false;
	public override bool CanWrite => false;
	public override long Length => data.Length;
	public override long Position { get => _position; set => throw new NotSupportedException(); }
	public override void Flush() { }
	public override int Read(byte[] buffer, int offset, int count)
	{
		int bytesToRead = Math.Min(count, Math.Min(maxChunkSize, data.Length - _position));
		if (bytesToRead <= 0) return 0;
		Buffer.BlockCopy(data, _position, buffer, offset, bytesToRead);
		_position += bytesToRead;
		return bytesToRead;
	}

	public override int Read(Span<byte> buffer)
	{
		int bytesToRead = Math.Min(buffer.Length, Math.Min(maxChunkSize, data.Length - _position));
		if (bytesToRead <= 0) return 0;
		data.AsSpan(_position, bytesToRead).CopyTo(buffer);
		_position += bytesToRead;
		return bytesToRead;
	}

	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		int bytesToRead = Math.Min(count, Math.Min(maxChunkSize, data.Length - _position));
		if (bytesToRead <= 0) return Task.FromResult(0);
		Buffer.BlockCopy(data, _position, buffer, offset, bytesToRead);
		_position += bytesToRead;
		return Task.FromResult(bytesToRead);
	}

	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		int bytesToRead = Math.Min(buffer.Length, Math.Min(maxChunkSize, data.Length - _position));
		if (bytesToRead <= 0) return ValueTask.FromResult(0);
		data.AsMemory(_position, bytesToRead).CopyTo(buffer);
		_position += bytesToRead;
		return ValueTask.FromResult(bytesToRead);
	}

	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

