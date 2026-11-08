namespace HanabePhotoManager.App.Services;

public enum HanabeSoundStyle { Camera, Cute, Mixed }
public enum HanabeSoundEvent { Start, Completed, Failure, Skipped, FeatureOpened, Canceled }

public sealed record HanabeSoundSettings(
    bool Enabled,
    HanabeSoundStyle Style,
    double Volume,
    bool QuietMode,
    bool StartEnabled = true,
    bool CompletionEnabled = true,
    bool FailureEnabled = true,
    bool SkipEnabled = true,
    bool OpenEnabled = false,
    bool CancelEnabled = true);

internal static class HanabeSoundPolicy
{
    internal static string? Resolve(HanabeAssistantState state, HanabeSoundSettings settings)
    {
        var kind = EventFor(state);
        if (kind is null || Resolve(kind.Value, settings) is null) return null;
        var name = state switch
        {
            HanabeAssistantState.Scanning => "scanning",
            HanabeAssistantState.Checking => "checking",
            HanabeAssistantState.Importing or HanabeAssistantState.Resuming => "importing",
            _ => AssetName(kind.Value)
        };
        return $"Assets/Hanabe/Sounds/{settings.Style}/{name}.wav";
    }

    internal static HanabeSoundEvent? EventFor(HanabeAssistantState state) => state switch
    {
        HanabeAssistantState.Scanning or HanabeAssistantState.Checking or HanabeAssistantState.Importing or HanabeAssistantState.Resuming => HanabeSoundEvent.Start,
        HanabeAssistantState.Completed => HanabeSoundEvent.Completed,
        HanabeAssistantState.Error or HanabeAssistantState.Failed or HanabeAssistantState.Interrupted or HanabeAssistantState.CompletedWithIssues => HanabeSoundEvent.Failure,
        HanabeAssistantState.Canceled => HanabeSoundEvent.Canceled,
        _ => null
    };

    internal static string? Resolve(HanabeSoundEvent kind, HanabeSoundSettings settings)
    {
        if (!settings.Enabled || settings.QuietMode && kind is not (HanabeSoundEvent.Completed or HanabeSoundEvent.Failure)) return null;
        var enabled = kind switch
        {
            HanabeSoundEvent.Start => settings.StartEnabled,
            HanabeSoundEvent.Completed => settings.CompletionEnabled,
            HanabeSoundEvent.Failure => settings.FailureEnabled,
            HanabeSoundEvent.Skipped => settings.SkipEnabled,
            HanabeSoundEvent.FeatureOpened => settings.OpenEnabled,
            HanabeSoundEvent.Canceled => settings.CancelEnabled,
            _ => false
        };
        return enabled ? $"Assets/Hanabe/Sounds/{settings.Style}/{AssetName(kind)}.wav" : null;
    }

    private static string AssetName(HanabeSoundEvent kind) => kind switch
    {
        HanabeSoundEvent.Start => "importing",
        HanabeSoundEvent.Completed => "completed",
        HanabeSoundEvent.Failure => "error",
        HanabeSoundEvent.Skipped => "skipped",
        HanabeSoundEvent.FeatureOpened => "opened",
        HanabeSoundEvent.Canceled => "canceled",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static double NormalizeVolume(double volume) => Math.Clamp(volume, 0, 100) / 100d;
}
