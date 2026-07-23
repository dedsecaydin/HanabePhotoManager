using System.IO;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.App.Services;

public sealed class LibraryMaintenanceService
{
    private static readonly Regex DateDirectoryPattern = new(
        @"^\d{1,2}\.\d{1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> IgnorableFileNames = new(
        ["Thumbs.db", "desktop.ini", ".DS_Store"],
        StringComparer.OrdinalIgnoreCase);

    public Task<LibraryMaintenanceResult> RemoveEmptyDateDirectoriesAsync(
        string libraryRoot,
        CancellationToken cancellationToken) =>
        Task.Run(() => RemoveEmptyDateDirectories(libraryRoot, cancellationToken), cancellationToken);

    private static LibraryMaintenanceResult RemoveEmptyDateDirectories(
        string libraryRoot,
        CancellationToken cancellationToken)
    {
        var deleted = new List<string>();
        var failures = new List<LibraryMaintenanceFailure>();
        if (string.IsNullOrWhiteSpace(libraryRoot) || !Directory.Exists(libraryRoot))
        {
            return new LibraryMaintenanceResult(deleted, failures);
        }

        string[] dateDirectories;
        try
        {
            dateDirectories = Directory.EnumerateDirectories(libraryRoot, "*", SearchOption.AllDirectories)
                .Where(IsDateDirectory)
                .OrderByDescending(path => path.Length)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add(new LibraryMaintenanceFailure(libraryRoot, ex.Message));
            return new LibraryMaintenanceResult(deleted, failures);
        }

        foreach (var dateDirectory in dateDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var files = Directory.EnumerateFiles(dateDirectory, "*", SearchOption.AllDirectories).ToArray();
                if (files.Any(path => !IgnorableFileNames.Contains(Path.GetFileName(path))))
                {
                    continue;
                }

                foreach (var junk in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Delete(junk);
                }

                foreach (var child in Directory.EnumerateDirectories(dateDirectory, "*", SearchOption.AllDirectories)
                             .OrderByDescending(path => path.Length))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Directory.EnumerateFileSystemEntries(child).Any()) Directory.Delete(child);
                }

                // Second check immediately before the irreversible operation.
                if (!Directory.EnumerateFileSystemEntries(dateDirectory).Any())
                {
                    Directory.Delete(dateDirectory);
                    deleted.Add(dateDirectory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(new LibraryMaintenanceFailure(dateDirectory, ex.Message));
            }
        }

        return new LibraryMaintenanceResult(deleted, failures);
    }

    private static bool IsDateDirectory(string path)
    {
        var name = Path.GetFileName(path);
        var parentName = Path.GetFileName(Path.GetDirectoryName(path));
        return DateDirectoryPattern.IsMatch(name) &&
               parentName?.EndsWith("月", StringComparison.Ordinal) == true;
    }
}

public sealed record LibraryMaintenanceResult(
    IReadOnlyList<string> Deleted,
    IReadOnlyList<LibraryMaintenanceFailure> Failures);

public sealed record LibraryMaintenanceFailure(string Path, string Reason);
