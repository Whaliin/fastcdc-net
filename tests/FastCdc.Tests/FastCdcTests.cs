namespace FastCdc.Tests;

public class FastCdcTests
{
    [Fact]
    public void FastCdc_TestAllZeros()
    {
        var array = new byte[10240];
        var options = new FastCdcOptions(64, 256, 1024);
        var results = FastCdc.Chunk(array, options).ToList();

        Assert.Equal(10, results.Count);
        foreach (var entry in results)
        {
            Assert.Equal(3106636015u, entry.Hash);
            Assert.Equal(0, entry.Offset % 1024);
            Assert.Equal(1024, entry.Length);
        }
    }

    [Fact]
    public void FastCdc_TestSekien16kChunks()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var results = FastCdc.Chunk(array, FastCdcOptions.Default16KB).ToList();

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
    public void FastCdc_TestSekien16kChunks_Streaming()
    {
        var filePath = "./fixtures/SekienAkashita.jpg";
        var array = File.ReadAllBytes(filePath);

        var chunkOffsets = new long[] { 0, 22366, 30648, 46951, 65647, 98415 };
        var chunkSizes = new int[] { 22366, 8282, 16303, 18696, 32768, 11051 };

        const int bufSize = 32768;
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;

        var filePos = 0;
        var chunkIndex = 0;

        var groupSizes = new[] { 2, 1, 1, 1, 1 };

        foreach (var groupSize in groupSizes)
        {
            var upperBound = filePos + bufSize;
            var (eof, slice) = upperBound >= fileSize
                ? (true, array[filePos..])
                : (false, array[filePos..upperBound]);

            var results = FastCdc.Chunk(slice, FastCdcOptions.Default16KB, eof).ToList();

            Assert.Equal(groupSize, results.Count);
            for (var idx = 0; idx < groupSize; idx++)
            {
                Assert.Equal(chunkOffsets[chunkIndex], results[idx].Offset + filePos);
                Assert.Equal(chunkSizes[chunkIndex], results[idx].Length);
                chunkIndex++;
            }

            foreach (var result in results)
                filePos += result.Length;
        }

        Assert.Equal(fileSize, filePos);
    }

    [Fact]
    public void FastCdc_TestSekien32kChunks()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var results = FastCdc.Chunk(array, FastCdcOptions.Default32KB).ToList();

        Assert.Equal(3, results.Count);

        Assert.Equal(0, results[0].Offset);
        Assert.Equal(32857, results[0].Length);
        Assert.Equal(2772598784u, results[0].Hash);

        Assert.Equal(32857, results[1].Offset);
        Assert.Equal(16408, results[1].Length);
        Assert.Equal(1651589120u, results[1].Hash);

        Assert.Equal(49265, results[2].Offset);
        Assert.Equal(60201, results[2].Length);
        Assert.Equal(1121740051u, results[2].Hash);
    }

    [Fact]
    public void FastCdc_TestSekien64kChunks()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var results = FastCdc.Chunk(array, FastCdcOptions.Default64KB).ToList();

        Assert.Equal(2, results.Count);

        Assert.Equal(0, results[0].Offset);
        Assert.Equal(32857, results[0].Length);
        Assert.Equal(2772598784u, results[0].Hash);

        Assert.Equal(32857, results[1].Offset);
        Assert.Equal(76609, results[1].Length);
        Assert.Equal(1121740051u, results[1].Hash);
    }
}
