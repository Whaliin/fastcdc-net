namespace FastCdc.Net.Tests;

public class ChunkEnumeratorTests
{
    [Fact]
    public void ChunkEnumerator_ToList_And_ToArray_MatchForeach()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var foreachChunks = new List<Chunk>();
        foreach (var chunk in FastCdc.GetChunks(array, 8192, 16384, 32768))
        {
            foreachChunks.Add(chunk);
        }

        var listChunks = FastCdc.GetChunks(array, 8192, 16384, 32768).ToList();
        var arrayChunks = FastCdc.GetChunks(array, 8192, 16384, 32768).ToArray();

        Assert.Equal(foreachChunks, listChunks);
        Assert.Equal(foreachChunks, arrayChunks);
    }
}

