using System.IO.Pipelines;
using System.Security.Cryptography;

namespace FastCdc.Tests;

public class FastCdcAsyncTests
{
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
		List<Chunk> expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		using MemoryStream stream = new(data, writable: false);
		List<Chunk> actual = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB, bufferSize: bufferSize))
		{
			actual.Add(chunk);
		}

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_EmptyStream_YieldsNoChunks()
	{
		using MemoryStream stream = new([]);
		List<Chunk> chunks = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(stream))
		{
			chunks.Add(chunk);
		}
		Assert.Empty(chunks);
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_CancellationToken_CancelsExecution()
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();
		using MemoryStream stream = new(new byte[10240]);

		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
		{
			await foreach (Chunk _ in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB, cancellationToken: cts.Token)) { }
		});
	}

	[Fact]
	public async Task FastCdc_ChunkAsync_PipeReader_MatchesSpan()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		List<Chunk> expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1024 * 1024));
		Task writerTask = Task.Run(async () =>
		{
			await pipe.Writer.WriteAsync(data);
			await pipe.Writer.CompleteAsync();
		});

		List<Chunk> actual = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB))
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

	[Fact]
	public async Task FastCdc_ChunkData_And_ChunkDataAsync_PayloadIntegrityAndDigests()
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		FastCdcOptions options = new(4096, 16384, 65534, FastCdcFormat.V2020);
		List<Chunk> expectedChunks = FastCdc.Chunk(data, options).ToList();

		// 1. In-memory ReadOnlyMemory<byte>
		List<Chunk> memoryChunks = new();
		using (MemoryStream reconstructed = new())
		{
			foreach (ChunkData chunkData in FastCdc.ChunkData(data, options))
			{
				Chunk metadata = chunkData;
				memoryChunks.Add(metadata);
				Assert.Equal(chunkData.Length, chunkData.Span.Length);
				Assert.Equal(chunkData.Span.ToArray(), chunkData.ToArray());
				reconstructed.Write(chunkData.Span);
			}
			Assert.Equal(data, reconstructed.ToArray());
		}
		Assert.Equal(expectedChunks, memoryChunks);

		// Verify SHA-256 digests of every chunk against the corresponding slice of the source file
		List<ChunkData> dataList = FastCdc.ChunkData(data, options).ToList();
		for (int i = 0; i < dataList.Count; i++)
		{
			byte[] expectedDigest = SHA256.HashData(data.AsSpan((int)dataList[i].Offset, dataList[i].Length));
			byte[] actualDigest = SHA256.HashData(dataList[i].Span);
			Assert.Equal(expectedDigest, actualDigest);
		}

		// 2. Synchronous Stream
		List<Chunk> syncStreamChunks = new();
		using (ChunkedReadStream syncStream = new(data, maxChunkSize: 251))
		using (MemoryStream reconstructed = new())
		{
			foreach (ChunkData chunkData in FastCdc.ChunkData(syncStream, options))
			{
				syncStreamChunks.Add(chunkData);
				reconstructed.Write(chunkData.Span);
			}
			Assert.Equal(data, reconstructed.ToArray());
		}
		Assert.Equal(expectedChunks, syncStreamChunks);

		// 3. Asynchronous Stream
		List<Chunk> asyncStreamChunks = new();
		using (ChunkedReadStream asyncStream = new(data, maxChunkSize: 251))
		using (MemoryStream reconstructed = new())
		{
			await foreach (ChunkData chunkData in FastCdc.ChunkDataAsync(asyncStream, options))
			{
				asyncStreamChunks.Add(chunkData);
				reconstructed.Write(chunkData.Span);
			}
			Assert.Equal(data, reconstructed.ToArray());
		}
		Assert.Equal(expectedChunks, asyncStreamChunks);

		// 4. PipeReader with small segments (multi-segment scratchBuffer path)
		Pipe pipe = new(new PipeOptions(minimumSegmentSize: 512, pauseWriterThreshold: 1024 * 1024));
		Task producer = Task.Run(async () =>
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

		List<Chunk> pipeChunks = new();
		using (MemoryStream reconstructed = new())
		{
			await foreach (ChunkData chunkData in FastCdc.ChunkDataAsync(pipe.Reader, options))
			{
				pipeChunks.Add(chunkData);
				reconstructed.Write(chunkData.Span);
			}
			Assert.Equal(data, reconstructed.ToArray());
		}
		await producer;
		Assert.Equal(expectedChunks, pipeChunks);

		// 5. PipeReader single large segment path
		Pipe singlePipe = new(new PipeOptions(minimumSegmentSize: 131072, pauseWriterThreshold: 1024 * 1024));
		await singlePipe.Writer.WriteAsync(data);
		await singlePipe.Writer.CompleteAsync();

		List<Chunk> singlePipeChunks = new();
		using (MemoryStream reconstructed = new())
		{
			await foreach (ChunkData chunkData in FastCdc.ChunkDataAsync(singlePipe.Reader, options))
			{
				singlePipeChunks.Add(chunkData);
				reconstructed.Write(chunkData.Span);
			}
			Assert.Equal(data, reconstructed.ToArray());
		}
		Assert.Equal(expectedChunks, singlePipeChunks);
	}
}
