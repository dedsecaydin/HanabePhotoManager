using System.Text.Json;
using System.Text.Json.Serialization;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed class JsonImportJournal
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task SaveAsync(ImportPlan plan, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidatePlanPaths(plan);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = fullPath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 1024 * 64,
                         options: FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await JsonSerializer.SerializeAsync(stream, plan, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(fullPath))
        {
            File.Replace(temporaryPath, fullPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, fullPath, overwrite: false);
        }
    }

    public async Task<ImportPlan?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var plan = await JsonSerializer.DeserializeAsync<ImportPlan>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        if (plan is not null)
        {
            ValidatePlanPaths(plan);
        }

        return plan;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new LibraryDateJsonConverter());
        return options;
    }

    private static void ValidatePlanPaths(ImportPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.LibraryRoot);
        var libraryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(plan.LibraryRoot));

        foreach (var item in plan.Items)
        {
            foreach (var file in item.Files)
            {
                ValidatePathUnderRoot(libraryRoot, file.DestinationPath, nameof(file.DestinationPath));
                ValidatePathUnderRoot(libraryRoot, file.TemporaryPath, nameof(file.TemporaryPath));
                if (!string.Equals(
                        Path.GetFullPath(file.TemporaryPath),
                        Path.GetFullPath(file.DestinationPath + ".hanabe-part"),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Journal temporary paths must match destination paths.");
                }
            }
        }
    }

    private static void ValidatePathUnderRoot(string libraryRoot, string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var rootWithSeparator = libraryRoot.EndsWith(Path.DirectorySeparatorChar)
            ? libraryRoot
            : libraryRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{parameterName} escapes the library root.");
        }
    }

    private sealed class LibraryDateJsonConverter : JsonConverter<LibraryDate>
    {
        public override LibraryDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("LibraryDate must be a JSON object.");
            }

            int? year = null;
            int? month = null;
            int? day = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (year is null || month is null || day is null)
                    {
                        throw new JsonException("LibraryDate requires year, month, and day.");
                    }

                    return new LibraryDate(year.Value, month.Value, day.Value);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected LibraryDate property name.");
                }

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "year":
                    case "Year":
                        year = reader.GetInt32();
                        break;
                    case "month":
                    case "Month":
                        month = reader.GetInt32();
                        break;
                    case "day":
                    case "Day":
                        day = reader.GetInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Unexpected end of LibraryDate JSON.");
        }

        public override void Write(Utf8JsonWriter writer, LibraryDate value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("year", value.Year);
            writer.WriteNumber("month", value.Month);
            writer.WriteNumber("day", value.Day);
            writer.WriteEndObject();
        }
    }
}
