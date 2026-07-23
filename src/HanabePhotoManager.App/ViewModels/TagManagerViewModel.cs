using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public sealed class TagManagerViewModel : ObservableObject
{
    private static readonly string[] BuiltInCategories =
    [
        "待分类", "人像", "自然风景", "城市风光", "建筑", "夜景",
        "动物", "美食", "植物", "室内", "交通", "其他"
    ];

    private readonly IMediaMetadataStore _store;

    public TagManagerViewModel(IMediaMetadataStore store)
    {
        _store = store;
        AvailableCategories = BuiltInCategories;
    }

    public IReadOnlyList<string> AvailableCategories { get; }

    public ObservableCollection<string> CustomTags { get; } = [];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        ReplaceCustomTags(snapshot.CustomTags ?? []);
    }

    public async Task CreateTagAsync(string? name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        if (normalized is null) return;

        var snapshot = await _store.LoadAsync(cancellationToken);
        snapshot.CustomTags ??= [];
        AddUnique(snapshot.CustomTags, normalized);
        await _store.SaveAsync(snapshot, cancellationToken);
        ReplaceCustomTags(snapshot.CustomTags);
    }

    public async Task RenameTagAsync(string? oldName, string? newName, CancellationToken cancellationToken = default)
    {
        var oldValue = NormalizeName(oldName);
        var newValue = NormalizeName(newName);
        if (oldValue is null || newValue is null) return;

        var snapshot = await _store.LoadAsync(cancellationToken);
        snapshot.CustomTags ??= [];
        snapshot.Entries ??= [];
        snapshot.CustomTags.RemoveAll(tag => EqualsName(tag, oldValue));
        AddUnique(snapshot.CustomTags, newValue);

        foreach (var entry in snapshot.Entries)
        {
            entry.ManualTags ??= [];
            if (entry.ManualTags.RemoveAll(tag => EqualsName(tag, oldValue)) > 0)
                AddUnique(entry.ManualTags, newValue);
        }

        await _store.SaveAsync(snapshot, cancellationToken);
        ReplaceCustomTags(snapshot.CustomTags);
    }

    public async Task DeleteTagAsync(string? name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        if (normalized is null) return;

        var snapshot = await _store.LoadAsync(cancellationToken);
        snapshot.CustomTags ??= [];
        snapshot.Entries ??= [];
        snapshot.CustomTags.RemoveAll(tag => EqualsName(tag, normalized));
        foreach (var entry in snapshot.Entries)
        {
            entry.ManualTags ??= [];
            entry.ManualTags.RemoveAll(tag => EqualsName(tag, normalized));
        }

        await _store.SaveAsync(snapshot, cancellationToken);
        ReplaceCustomTags(snapshot.CustomTags);
    }

    public async Task AssignTagAsync(IEnumerable<string> paths, string? tag, CancellationToken cancellationToken = default)
    {
        var normalizedTag = NormalizeName(tag);
        if (normalizedTag is null) return;

        var normalizedPaths = NormalizePaths(paths);
        if (normalizedPaths.Count == 0) return;

        var snapshot = await _store.LoadAsync(cancellationToken);
        snapshot.CustomTags ??= [];
        snapshot.Entries ??= [];
        AddUnique(snapshot.CustomTags, normalizedTag);
        foreach (var path in normalizedPaths)
        {
            var entry = GetOrCreate(snapshot, path);
            entry.ManualTags ??= [];
            AddUnique(entry.ManualTags, normalizedTag);
        }

        await _store.SaveAsync(snapshot, cancellationToken);
        ReplaceCustomTags(snapshot.CustomTags);
    }

    public async Task RemoveTagAsync(IEnumerable<string> paths, string? tag, CancellationToken cancellationToken = default)
    {
        var normalizedTag = NormalizeName(tag);
        if (normalizedTag is null) return;
        var normalizedPaths = NormalizePaths(paths);

        var snapshot = await _store.LoadAsync(cancellationToken);
        snapshot.Entries ??= [];
        foreach (var entry in snapshot.Entries.Where(entry => normalizedPaths.Contains(entry.Path)))
        {
            entry.ManualTags ??= [];
            entry.ManualTags.RemoveAll(candidate => EqualsName(candidate, normalizedTag));
        }
        await _store.SaveAsync(snapshot, cancellationToken);
    }

    public async Task SetManualCategoryAsync(IEnumerable<string> paths, string? category, CancellationToken cancellationToken = default)
    {
        var normalizedCategory = NormalizeName(category);
        if (normalizedCategory is null) return;
        var normalizedPaths = NormalizePaths(paths);
        if (normalizedPaths.Count == 0) return;

        var snapshot = await _store.LoadAsync(cancellationToken);
        snapshot.Entries ??= [];
        foreach (var path in normalizedPaths)
            GetOrCreate(snapshot, path).ManualCategory = normalizedCategory;
        await _store.SaveAsync(snapshot, cancellationToken);
    }

    private void ReplaceCustomTags(IEnumerable<string> tags)
    {
        CustomTags.Clear();
        foreach (var tag in tags
                     .Select(NormalizeName)
                     .Where(tag => tag is not null)
                     .Select(tag => tag!)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase))
            CustomTags.Add(tag);
    }

    private static MediaMetadataEntry GetOrCreate(MediaMetadataSnapshot snapshot, string path)
    {
        var entry = snapshot.Entries.FirstOrDefault(candidate => EqualsName(candidate.Path, path));
        if (entry is not null) return entry;
        entry = new MediaMetadataEntry { Path = path };
        snapshot.Entries.Add(entry);
        return entry;
    }

    private static HashSet<string> NormalizePaths(IEnumerable<string> paths) =>
        paths.Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Any(candidate => EqualsName(candidate, value))) values.Add(value);
    }

    private static string? NormalizeName(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool EqualsName(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
