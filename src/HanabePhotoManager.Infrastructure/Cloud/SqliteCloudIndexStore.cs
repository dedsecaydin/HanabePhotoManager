using System.Diagnostics;
using System.Globalization;
using HanabePhotoManager.Core.Cloud;
using Microsoft.Data.Sqlite;

namespace HanabePhotoManager.Infrastructure.Cloud;

public sealed class SqliteCloudIndexStore : ICloudIndexStore
{
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan BusyRetryLimit = TimeSpan.FromSeconds(30);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS cloud_objects (
          provider INTEGER NOT NULL,
          remote_id TEXT NOT NULL,
          parent_path TEXT NOT NULL,
          full_path TEXT NOT NULL,
          name TEXT NOT NULL,
          kind INTEGER NOT NULL,
          size INTEGER NOT NULL,
          modified_at TEXT NOT NULL,
          thumbnail_key TEXT NULL,
          is_hanabe_managed INTEGER NOT NULL,
          PRIMARY KEY(provider, remote_id)
        );
        CREATE INDEX IF NOT EXISTS ix_cloud_children
          ON cloud_objects(provider, parent_path, name);
        """;

    private const string UpsertSql =
        """
        INSERT INTO cloud_objects (
          provider, remote_id, parent_path, full_path, name, kind, size,
          modified_at, thumbnail_key, is_hanabe_managed)
        VALUES (
          $provider, $remoteId, $parentPath, $fullPath, $name, $kind, $size,
          $modifiedAt, $thumbnailKey, $isHanabeManaged)
        ON CONFLICT(provider, remote_id) DO UPDATE SET
          parent_path = excluded.parent_path,
          full_path = excluded.full_path,
          name = excluded.name,
          kind = excluded.kind,
          size = excluded.size,
          modified_at = excluded.modified_at,
          thumbnail_key = excluded.thumbnail_key,
          is_hanabe_managed = excluded.is_hanabe_managed;
        """;

    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly object _queueLock = new();
    private Task _operationTail = Task.CompletedTask;
    private bool _initialized;

    public SqliteCloudIndexStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Cloud index database path is required.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 1
        }.ToString();
    }

    public Task UpsertAsync(
        IEnumerable<CloudObject> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        return EnqueueAsync(
            async token =>
            {
                await EnsureInitializedAsync(token).ConfigureAwait(false);
                using var connection = OpenConnection(token);
                using var interruptRegistration = RegisterInterrupt(connection, token);
                return await ExecuteWriteTransactionAsync(
                        connection,
                        async () =>
                        {
                            using var command = connection.CreateCommand();
                            command.CommandText = UpsertSql;
                            var provider = command.Parameters.Add("$provider", SqliteType.Integer);
                            var remoteId = command.Parameters.Add("$remoteId", SqliteType.Text);
                            var parentPath = command.Parameters.Add("$parentPath", SqliteType.Text);
                            var fullPath = command.Parameters.Add("$fullPath", SqliteType.Text);
                            var name = command.Parameters.Add("$name", SqliteType.Text);
                            var kind = command.Parameters.Add("$kind", SqliteType.Integer);
                            var size = command.Parameters.Add("$size", SqliteType.Integer);
                            var modifiedAt = command.Parameters.Add("$modifiedAt", SqliteType.Text);
                            var thumbnailKey = command.Parameters.Add("$thumbnailKey", SqliteType.Text);
                            var isHanabeManaged = command.Parameters.Add("$isHanabeManaged", SqliteType.Integer);

                            foreach (var item in items)
                            {
                                token.ThrowIfCancellationRequested();
                                if (item is null)
                                {
                                    throw new ArgumentException(
                                        "Cloud index items cannot contain null.",
                                        nameof(items));
                                }

                                provider.Value = (int)item.Provider;
                                remoteId.Value = item.RemoteId;
                                parentPath.Value = GetParentPath(item.Path);
                                fullPath.Value = item.Path.Value;
                                name.Value = item.Name;
                                kind.Value = (int)item.Kind;
                                size.Value = item.Size;
                                modifiedAt.Value = item.ModifiedAt.ToString("O", CultureInfo.InvariantCulture);
                                thumbnailKey.Value = item.ThumbnailKey is null ? DBNull.Value : item.ThumbnailKey;
                                isHanabeManaged.Value = item.IsHanabeManaged ? 1 : 0;
                                await ExecuteWithBusyRetryAsync(command.ExecuteNonQuery, token)
                                    .ConfigureAwait(false);
                            }

                            return true;
                        },
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<CloudObject>> QueryChildrenAsync(
        CloudProviderKind provider,
        CloudPath directory,
        CancellationToken cancellationToken = default)
    {
        ValidateProvider(provider);
        ArgumentNullException.ThrowIfNull(directory);
        return EnqueueAsync<IReadOnlyList<CloudObject>>(
            async token =>
            {
                await EnsureInitializedAsync(token).ConfigureAwait(false);
                using var connection = OpenConnection(token);
                using var interruptRegistration = RegisterInterrupt(connection, token);
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT provider, remote_id, full_path, name, kind, size, modified_at,
                           thumbnail_key, is_hanabe_managed
                    FROM cloud_objects
                    WHERE provider = $provider AND parent_path = $parentPath;
                    """;
                command.Parameters.AddWithValue("$provider", (int)provider);
                command.Parameters.AddWithValue("$parentPath", directory.Value);

                var items = new List<CloudObject>();
                using var reader = await ExecuteWithBusyRetryAsync(command.ExecuteReader, token)
                    .ConfigureAwait(false);
                while (await ExecuteWithBusyRetryAsync(reader.Read, token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    var managedValue = reader.GetInt64(8);
                    if (managedValue is not (0 or 1))
                    {
                        throw new InvalidDataException("Cloud index contains an invalid Hanabe-managed flag.");
                    }

                    items.Add(new CloudObject(
                        (CloudProviderKind)reader.GetInt32(0),
                        reader.GetString(1),
                        new CloudPath(reader.GetString(2)),
                        reader.GetString(3),
                        (CloudObjectKind)reader.GetInt32(4),
                        reader.GetInt64(5),
                        DateTimeOffset.ParseExact(
                            reader.GetString(6),
                            "O",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind),
                        reader.IsDBNull(7) ? null : reader.GetString(7),
                        managedValue == 1));
                }

                return Array.AsReadOnly(items
                    .OrderBy(static item => item.Kind == CloudObjectKind.Folder ? 0 : 1)
                    .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static item => item.Name, StringComparer.Ordinal)
                    .ThenBy(static item => item.Path.Value, StringComparer.Ordinal)
                    .ThenBy(static item => item.RemoteId, StringComparer.Ordinal)
                    .ToArray());
            },
            cancellationToken);
    }

    public Task RemoveProviderAsync(
        CloudProviderKind provider,
        CancellationToken cancellationToken = default)
    {
        ValidateProvider(provider);
        return EnqueueAsync(
            async token =>
            {
                await EnsureInitializedAsync(token).ConfigureAwait(false);
                using var connection = OpenConnection(token);
                using var interruptRegistration = RegisterInterrupt(connection, token);
                return await ExecuteWriteTransactionAsync(
                        connection,
                        async () =>
                        {
                            using var command = connection.CreateCommand();
                            command.CommandText = "DELETE FROM cloud_objects WHERE provider = $provider;";
                            command.Parameters.AddWithValue("$provider", (int)provider);
                            await ExecuteWithBusyRetryAsync(command.ExecuteNonQuery, token)
                                .ConfigureAwait(false);
                            return true;
                        },
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    private Task<T> EnqueueAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var orderingCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var orderingTask = orderingCompletion.Task;
        var cancellationState = new QueueCancellationState<T>(completion, cancellationToken);
        var cancellationRegistration = cancellationToken.UnsafeRegister(
            static state => ((QueueCancellationState<T>)state!).Cancel(),
            cancellationState);
        Task predecessor;
        lock (_queueLock)
        {
            predecessor = _operationTail;
            _operationTail = orderingTask;
        }

        _ = ProcessQueuedOperationAsync(
            predecessor,
            orderingTask,
            orderingCompletion,
            operation,
            cancellationToken,
            cancellationRegistration,
            completion);
        return completion.Task;
    }

    private async Task ProcessQueuedOperationAsync<T>(
        Task predecessor,
        Task orderingTask,
        TaskCompletionSource<bool> orderingCompletion,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        CancellationTokenRegistration cancellationRegistration,
        TaskCompletionSource<T> completion)
    {
        T? result = default;
        Exception? operationException = null;
        var hasResult = false;
        var wasCanceled = false;
        try
        {
            await predecessor.ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                wasCanceled = true;
            }
            else
            {
                result = await Task.Run(
                        () => operation(cancellationToken),
                        CancellationToken.None)
                    .ConfigureAwait(false);
                hasResult = true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            wasCanceled = true;
        }
        catch (Exception exception)
        {
            operationException = exception;
        }
        finally
        {
            cancellationRegistration.Dispose();
            orderingCompletion.TrySetResult(true);
            ResetCompletedTail(orderingTask);
        }

        if (wasCanceled)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        else if (operationException is not null)
        {
            completion.TrySetException(operationException);
        }
        else if (hasResult)
        {
            completion.TrySetResult(result!);
        }
    }

    private void ResetCompletedTail(Task orderingTask)
    {
        lock (_queueLock)
        {
            if (ReferenceEquals(_operationTail, orderingTask))
            {
                _operationTail = Task.CompletedTask;
            }
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection(cancellationToken);
        using var interruptRegistration = RegisterInterrupt(connection, cancellationToken);
        await ExecuteWriteTransactionAsync(
                connection,
                async () =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = SchemaSql;
                    await ExecuteWithBusyRetryAsync(command.ExecuteNonQuery, cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);
        _initialized = true;
    }

    private SqliteConnection OpenConnection(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            var busyTimeoutResult = SQLitePCL.raw.sqlite3_busy_timeout(connection.Handle, 25);
            SqliteException.ThrowExceptionForRC(busyTimeoutResult, connection.Handle);
            cancellationToken.ThrowIfCancellationRequested();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static async Task<T> ExecuteWithBusyRetryAsync<T>(
        Func<T> operation,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return operation();
            }
            catch (SqliteException exception) when (
                IsBusy(exception) && Stopwatch.GetElapsedTime(startedAt) < BusyRetryLimit)
            {
                await Task.Delay(BusyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (
                cancellationToken.IsCancellationRequested &&
                exception.SqliteErrorCode is 5 or 6 or 9)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }

    private static async Task<T> ExecuteWriteTransactionAsync<T>(
        SqliteConnection connection,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteRawSqlWithBusyRetryAsync(connection, "BEGIN IMMEDIATE;", cancellationToken)
            .ConfigureAwait(false);
        var committed = false;
        Exception? primaryException = null;
        try
        {
            var result = await operation().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteRawSqlWithBusyRetryAsync(connection, "COMMIT;", cancellationToken)
                .ConfigureAwait(false);
            committed = true;
            return result;
        }
        catch (Exception exception)
        {
            primaryException = exception;
            throw;
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    ExecuteRawSql(connection, "ROLLBACK;");
                }
                catch (Exception rollbackException) when (primaryException is not null)
                {
                    primaryException.Data["RollbackException"] = rollbackException;
                }
            }
        }
    }

    private static Task<bool> ExecuteRawSqlWithBusyRetryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken) =>
        ExecuteWithBusyRetryAsync(
            () =>
            {
                ExecuteRawSql(connection, sql);
                return true;
            },
            cancellationToken);

    private static void ExecuteRawSql(SqliteConnection connection, string sql)
    {
        var result = SQLitePCL.raw.sqlite3_exec(connection.Handle, sql);
        SqliteException.ThrowExceptionForRC(result, connection.Handle);
    }

    private static CancellationTokenRegistration RegisterInterrupt(
        SqliteConnection connection,
        CancellationToken cancellationToken) =>
        cancellationToken.Register(
            static state => SQLitePCL.raw.sqlite3_interrupt(((SqliteConnection)state!).Handle),
            connection);

    private static bool IsBusy(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;

    private static string GetParentPath(CloudPath path)
    {
        var lastSeparator = path.Value.LastIndexOf('/');
        return lastSeparator <= 0 ? "/" : path.Value[..lastSeparator];
    }

    private static void ValidateProvider(CloudProviderKind provider)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "Cloud provider is undefined.");
        }
    }

    private sealed class QueueCancellationState<T>(
        TaskCompletionSource<T> completion,
        CancellationToken cancellationToken)
    {
        public void Cancel() => completion.TrySetCanceled(cancellationToken);
    }
}
