using System.Buffers.Binary;
using HanabePhotoManager.Core.Search;
using Microsoft.Data.Sqlite;

namespace HanabePhotoManager.Infrastructure.Search;

public sealed class SqliteSemanticIndexStore : ISemanticIndexStore
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS semantic_index (
          file_key TEXT PRIMARY KEY,
          fingerprint TEXT NOT NULL,
          modified_at TEXT NOT NULL,
          embedding BLOB NOT NULL
        );
        """;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteSemanticIndexStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    }

    public Task UpsertAsync(IReadOnlyList<SemanticIndexEntry> entries, CancellationToken cancellationToken) => ExecuteAsync(async connection =>
    {
        using var transaction = connection.BeginTransaction();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO semantic_index(file_key, fingerprint, modified_at, embedding)
                VALUES ($fileKey, $fingerprint, $modifiedAt, $embedding)
                ON CONFLICT(file_key) DO UPDATE SET fingerprint=excluded.fingerprint, modified_at=excluded.modified_at, embedding=excluded.embedding;
                """;
            command.Parameters.AddWithValue("$fileKey", entry.FileKey);
            command.Parameters.AddWithValue("$fingerprint", entry.Fingerprint);
            command.Parameters.AddWithValue("$modifiedAt", entry.ModifiedAtUtc.ToString("O"));
            command.Parameters.Add("$embedding", SqliteType.Blob).Value = ToBytes(entry.Embedding);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }, cancellationToken);

    public Task<IReadOnlyList<SemanticIndexEntry>> GetAllAsync(CancellationToken cancellationToken) => ExecuteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT file_key, fingerprint, modified_at, embedding FROM semantic_index;";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<SemanticIndexEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            entries.Add(new SemanticIndexEntry(reader.GetString(0), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2)), FromBytes((byte[])reader[3])));
        return (IReadOnlyList<SemanticIndexEntry>)entries;
    }, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) => ExecuteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM semantic_index;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }, cancellationToken);

    public Task RemoveMissingAsync(IEnumerable<string> existingPaths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(existingPaths);
        var paths = existingPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return ExecuteAsync(async connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var clear = connection.CreateCommand();
            clear.Transaction = transaction;
            clear.CommandText = "CREATE TEMP TABLE IF NOT EXISTS semantic_existing(file_key TEXT PRIMARY KEY); DELETE FROM semantic_existing;";
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            foreach (var path in paths)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT OR IGNORE INTO semantic_existing(file_key) VALUES ($fileKey);";
                insert.Parameters.AddWithValue("$fileKey", path);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            using var remove = connection.CreateCommand();
            remove.Transaction = transaction;
            remove.CommandText = "DELETE FROM semantic_index WHERE file_key NOT IN (SELECT file_key FROM semantic_existing);";
            await remove.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    private async Task ExecuteAsync(Func<SqliteConnection, Task> operation, CancellationToken cancellationToken) =>
        await ExecuteAsync(async connection => { await operation(connection).ConfigureAwait(false); return true; }, cancellationToken).ConfigureAwait(false);

    private async Task<T> ExecuteAsync<T>(Func<SqliteConnection, Task<T>> operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var schema = connection.CreateCommand();
            schema.CommandText = Schema;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return await operation(connection).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static byte[] ToBytes(IReadOnlyList<float> values)
    {
        var bytes = new byte[values.Count * sizeof(float)];
        for (var index = 0; index < values.Count; index++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * sizeof(float)), values[index]);
        return bytes;
    }

    private static float[] FromBytes(byte[] bytes)
    {
        if (bytes.Length % sizeof(float) != 0) throw new InvalidDataException("Semantic embedding is corrupt.");
        var values = new float[bytes.Length / sizeof(float)];
        for (var index = 0; index < values.Length; index++) values[index] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(index * sizeof(float)));
        return values;
    }
}
