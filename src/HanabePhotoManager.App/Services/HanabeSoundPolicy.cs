namespace HanabePhotoManager.App.Services;

public enum HanabeSoundStyle { Camera, Cute, Mixed }

public sealed record HanabeSoundSettings(
    bool Enabled,
    HanabeSoundStyle Style,
    double Volume,
    bool QuietMode);

internal static class HanabeSoundPolicy
{
    internal static string? Resolve(HanabeAssistantState state, HanabeSoundSettings settings)
    {
        if (!settings.Enabled || state == HanabeAssistantState.Idle) return null;
        if (settings.QuietMode && state is not (HanabeAssistantState.Completed or HanabeAssistantState.Error)) return null;
        return $"Assets/Hanabe/Sounds/{settings.Style}/{state.ToString().ToLowerInvariant()}.wav";
    }

    internal static double NormalizeVolume(double volume) => Math.Clamp(volume, 0, 100) / 100d;
}
