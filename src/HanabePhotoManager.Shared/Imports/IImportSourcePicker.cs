namespace HanabePhotoManager.App.Imports;

public interface IImportSourcePicker
{
    IReadOnlyList<string> PickFolders(string initialDirectory);
}
