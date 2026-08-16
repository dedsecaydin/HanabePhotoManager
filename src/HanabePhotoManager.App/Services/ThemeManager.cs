using System.IO;
using System.Windows;

namespace HanabePhotoManager.App.Services;

public enum AppTheme { Light, Dark }

/// <summary>
/// 四套 M3 配色方案（动态色彩 / 森林绿 / 紫罗兰 / 经典中性）。与 <see cref="AppTheme"/> 明暗维度组合成 8 套主题。
/// </summary>
public enum AppColorScheme { Dynamic, Forest, Violet, Classic }

public static class ThemeManager
{
    private static readonly string PreferencePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager", "ui-theme.txt");
    public static AppTheme Current { get; private set; } = AppTheme.Light;
    public static AppColorScheme CurrentScheme { get; private set; } = AppColorScheme.Violet;
    public static event EventHandler<AppTheme>? ThemeChanged;
    public static AppTheme ParsePreference(string? value) => string.Equals(value?.Trim(), "dark", StringComparison.OrdinalIgnoreCase) ? AppTheme.Dark : AppTheme.Light;

    public static AppColorScheme ParseSchemePreference(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "forest" => AppColorScheme.Forest,
        "violet" => AppColorScheme.Violet,
        "classic" => AppColorScheme.Classic,
        _ => AppColorScheme.Violet,
    };

    public static void LoadAndApply()
    {
        string? preference = File.Exists(PreferencePath) ? File.ReadAllText(PreferencePath) : null;
        var (theme, scheme) = ParseCombinedPreference(preference);
        Apply(theme, scheme, persist: false);
    }

    public static void Toggle() => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    /// <summary>切换明暗，保留当前配色方案。</summary>
    public static void Apply(AppTheme theme, bool persist = true) => Apply(theme, CurrentScheme, persist);

    /// <summary>切换配色方案，保留当前明暗。</summary>
    public static void Apply(AppColorScheme scheme, bool persist = true) => Apply(Current, scheme, persist);

    public static void Apply(AppTheme theme, AppColorScheme scheme, bool persist = true)
    {
        var modeChanged = Current != theme;
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains("Themes/Themes/", StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary { Source = new Uri($"/HanabePhotoManager.App;component/Themes/Themes/{scheme}.{theme}.xaml", UriKind.RelativeOrAbsolute) };
        if (existing is null) dictionaries.Insert(0, replacement); else dictionaries[dictionaries.IndexOf(existing)] = replacement;
        Current = theme;
        CurrentScheme = scheme;
        if (persist)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, $"{scheme}.{theme}");
        }
        if (modeChanged)
        {
            ThemeChanged?.Invoke(null, theme);
        }
    }

    /// <summary>
    /// 解析持久化的主题偏好：新格式为「Scheme.Mode」（如 Forest.Dark）；
    /// 旧格式仅「Light/Dark」，回退为 Dynamic 配色。
    /// </summary>
    private static (AppTheme Theme, AppColorScheme Scheme) ParseCombinedPreference(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return (AppTheme.Light, AppColorScheme.Violet);
        var parts = trimmed.Split('.');
        return parts.Length == 2
            ? (ParsePreference(parts[1]), ParseSchemePreference(parts[0]))
            : (ParsePreference(trimmed), AppColorScheme.Violet);
    }
}
