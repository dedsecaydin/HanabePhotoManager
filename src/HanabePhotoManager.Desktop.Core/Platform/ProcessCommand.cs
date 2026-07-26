namespace HanabePhotoManager.Desktop.Core.Platform;

public sealed record ProcessCommand(string FileName, IReadOnlyList<string> Arguments);
