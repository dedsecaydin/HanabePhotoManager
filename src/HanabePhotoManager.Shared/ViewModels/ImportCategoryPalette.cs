namespace HanabePhotoManager.App.ViewModels;

/// <summary>Portable category colors shared by import presentations.</summary>
public static class ImportCategoryPalette
{
    public static string For(string category) => category switch
    {
        var c when c.Contains("RAW", StringComparison.OrdinalIgnoreCase) => "#FFB9A2FF",
        var c when c.Contains("JPG", StringComparison.OrdinalIgnoreCase) => "#FFFF8FB8",
        var c when c.Contains("action", StringComparison.OrdinalIgnoreCase) => "#FF6FB7FF",
        var c when c.Contains("视频") => "#FFFFB36B",
        var c when c.Contains("修后") => "#FF7EE0C3",
        var c when c.Contains("素材") => "#FFC9C9D6",
        _ => "#FF9A96A8",
    };
}
