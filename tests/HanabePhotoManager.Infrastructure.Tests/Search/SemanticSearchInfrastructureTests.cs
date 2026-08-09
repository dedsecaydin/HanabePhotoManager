using HanabePhotoManager.Core.Search;
using HanabePhotoManager.Infrastructure.Search;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace HanabePhotoManager.Infrastructure.Tests.Search;

public sealed class SemanticSearchInfrastructureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-semantic-{Guid.NewGuid():N}");

    public SemanticSearchInfrastructureTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Tokenize_ChineseAndEnglish_UsesVocabularyAndPadding()
    {
        var vocabulary = Path.Combine(_directory, "vocab.txt");
        File.WriteAllLines(vocabulary, ["[PAD]", "[UNK]", "[CLS]", "[SEP]", "海", "边", "sun", "##set"]);
        var result = new ClipTokenizer(vocabulary).Tokenize("海边 sunset", 8);
        result.TokenIds.Should().Equal(2, 4, 5, 6, 7, 3, 0, 0);
        result.AttentionMask.Should().Equal(1, 1, 1, 1, 1, 1, 0, 0);
    }

    [Fact]
    public async Task Store_UpsertsAndRemovesMissingEntries()
    {
        var store = new SqliteSemanticIndexStore(Path.Combine(_directory, "semantic.db"));
        await store.UpsertAsync([
            new SemanticIndexEntry("keep.jpg", "1", DateTimeOffset.UnixEpoch, [1f, 0f]),
            new SemanticIndexEntry("remove.jpg", "2", DateTimeOffset.UnixEpoch, [0f, 1f])], CancellationToken.None);
        await store.RemoveMissingAsync(["keep.jpg"], CancellationToken.None);
        var entries = await store.GetAllAsync(CancellationToken.None);
        entries.Should().ContainSingle().Which.FileKey.Should().Be("keep.jpg");
        entries[0].Embedding.Should().Equal(1f, 0f);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
