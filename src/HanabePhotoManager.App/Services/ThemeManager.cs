using System.IO;
using System.Windows;

namespace HanabePhotoManager.App.Services;

public enum AppTheme { Light, Dark }

public static class ThemeManager
{
    private static readonly string PreferencePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager", "ui-theme.txt");
    public static AppTheme Current { get; private set; } = AppTheme.Light;
    public static AppTheme ParsePreference(string? value) => string.Equals(value?.Trim(), "dark", StringComparison.OrdinalIgnoreCase) ? AppTheme.Dark : AppTheme.Light;

    public static void LoadAndApply()
    {
        string? preference = File.Exists(PreferencePath) ? File.ReadAllText(PreferencePath) : null;
        Apply(ParsePreference(preference), false);
    }

    public static void Toggle() => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    public static void Apply(AppTheme theme, bool persist = true)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains("Themes/Themes/", StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary { Source = new Uri($"/HanabePhotoManager.App;component/Themes/Themes/{theme}.xaml", UriKind.RelativeOrAbsolute) };
        if (existing is null) dictionaries.Insert(0, replacement); else dictionaries[dictionaries.IndexOf(existing)] = replacement;
        Current = theme;
        if (!persist) return;
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        File.WriteAllText(PreferencePath, theme.ToString());
    }
}
