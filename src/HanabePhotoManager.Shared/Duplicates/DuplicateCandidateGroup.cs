namespace HanabePhotoManager.App;

/// <summary>
/// A group of files that are either confirmed or suspected duplicates.
/// </summary>
/// <param name="Paths">
/// The file paths that belong to this duplicate group (always 2 or more).
/// </param>
/// <param name="IsSuspected">
/// True when the group was matched by visual similarity (perceptual hash) instead
/// of an exact content hash. This is lower confidence — the user should review
/// these before deleting, so the review window labels them as 疑似 (suspected).
/// </param>
public sealed record DuplicateCandidateGroup(IReadOnlyList<string> Paths, bool IsSuspected);
