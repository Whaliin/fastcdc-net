using System.IO.Pipelines;

namespace FastCdc.Tests;

public class FastCdcAsyncTests
{
	[Fact]
	public async Task FastCdc_TestSekien16kChunks_Async()
	{
		await using var stream = File.OpenRead("./fixtures/SekienAkashita.jpg");
		var results = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB))
			results.Add(chunk);

		Assert.Equal(6, results.Count);

		Assert.Equal(0, results[0].Offset);
		Assert.Equal(22366, results[0].Length);
		Assert.Equal(1527472128u, results[0].Hash);

		Assert.Equal(22366, results[1].Offset);
		Assert.Equal(8282, results[1].Length);
		Assert.Equal(1174757376u, results[1].Hash);

		Assert.Equal(30648, results[2].Offset);
		Assert.Equal(16303, results[2].Length);
		Assert.Equal(2687197184u, results[2].Hash);

		Assert.Equal(46951, results[3].Offset);
		Assert.Equal(18696, results[3].Length);
		Assert.Equal(1210105856u, results[3].Hash);

		Assert.Equal(65647, results[4].Offset);
		Assert.Equal(32768, results[4].Length);
		Assert.Equal(2984739645u, results[4].Hash);

		Assert.Equal(98415, results[5].Offset);
		Assert.Equal(11051, results[5].Length);
		Assert.Equal(1121740051u, results[5].Hash);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_MatchesSpan_Sekien32k()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		var expected = FastCdc.Chunk(data, FastCdcOptions.Default32KB).ToList();

		using var stream = new MemoryStream(data, writable: false);
		var actual = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default32KB))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_MatchesSpan_Sekien64k()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		var expected = FastCdc.Chunk(data, FastCdcOptions.Default64KB).ToList();

		using var stream = new MemoryStream(data, writable: false);
		var actual = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default64KB))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(512)]
	[InlineData(1024)]
	[InlineData(8192)]
	[InlineData(32768)]
	[InlineData(65536)]
	public async Task FastCdc_ChunkAsync_DifferentBufferSizes_MatchExpected(int bufferSize)
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		var expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		using var stream = new MemoryStream(data, writable: false);
		var actual = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB, bufferSize: bufferSize))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_ChunkedStreamReads_MatchesSpan()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		var expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		using var stream = new ChunkedReadStream(data, maxChunkSize: 127);
		var actual = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB, bufferSize: 1024))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_AllZeros_MatchesSpan()
	{
		byte[] data = new byte[10240];
		var options = new FastCdcOptions(64, 256, 1024);
		var expected = FastCdc.Chunk(data, options).ToList();

		using var stream = new MemoryStream(data, writable: false);
		var actual = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream, options))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_EmptyStream_YieldsNoChunks()
	{
		using var stream = new MemoryStream([]);
		var chunks = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(stream))
		{
			chunks.Add(chunk);
		}
		Assert.Empty(chunks);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_CancellationToken_CancelsExecution()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		using var stream = new MemoryStream(new byte[10240]);

		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB, cancellationToken: cts.Token)) { }
		});
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_PipeReader_MatchesSpan()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		var expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1024 * 1024));
		// Write data into the pipe writer in background
		Task writerTask = Task.Run(async () =>
		{
			await pipe.Writer.WriteAsync(data);
			await pipe.Writer.CompleteAsync();
		});

		var actual = new List<Chunk>();
		await foreach (var chunk in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB))
		{
			actual.Add(chunk);
		}

		await writerTask;
		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_PipeReader_CompletedTerminalBuffer_MatchesSpan()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		List<Chunk> expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		// Write the entire payload and complete the writer BEFORE starting ChunkAsync.
		// This tests a completed multi-chunk terminal buffer (> MaxSize) right on the first ReadAsync.
		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1024 * 1024));
		await pipe.Writer.WriteAsync(data);
		await pipe.Writer.CompleteAsync();

		List<Chunk> actual = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_PipeReader_SmallSegmentsForced_MatchesSpan()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		List<Chunk> expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		// Force small segments (512 bytes) so that FirstSpan is always < MaxSize (32 KB),
		// exercising the cross-segment scratch buffer copy path for every chunk.
		PipeOptions pipeOptions = new(minimumSegmentSize: 512, pauseWriterThreshold: 1024 * 1024);
		Pipe pipe = new(pipeOptions);

		Task producerTask = Task.Run(async () =>
		{
			int sent = 0;
			while (sent < data.Length)
			{
				int toWrite = Math.Min(512, data.Length - sent);
				await pipe.Writer.WriteAsync(data.AsMemory(sent, toWrite));
				sent += toWrite;
			}
			await pipe.Writer.CompleteAsync();
		});

		List<Chunk> actual = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB))
		{
			actual.Add(chunk);
		}

		await producerTask;
		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_PipeReader_CancellationMidStream_Cancels()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1024 * 1024));
		await pipe.Writer.WriteAsync(data);
		await pipe.Writer.CompleteAsync();

		using CancellationTokenSource cts = new();

		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
		{
			int count = 0;
			await foreach (Chunk _ in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB, cancellationToken: cts.Token))
			{
				count++;
				if (count == 2)
				{
					cts.Cancel();
				}
			}
		});
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_PipeReader_WriterException_Propagates()
	{
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(new byte[100]);
		pipe.Writer.Complete(new InvalidOperationException("Pipe producer failure"));

		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await foreach (Chunk _ in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB))
			{
			}
		});
	}
}
