namespace HanabePhotoManager.App.Services;

public enum HanabeAssistantState { Idle, Scanning, Checking, Importing, Completed, Error }

public enum HanabeAssistantVisualStyle { ChibiAnimated, PixelAnimated, Static, Off }

internal static class HanabeAssistantAnimationResolver
{
    internal static string Resolve(HanabeAssistantVisualStyle style, HanabeAssistantState state)
    {
        if (style == HanabeAssistantVisualStyle.Static) return "Assets/Hanabe/hanabe-assistant.png";
        var folder = style == HanabeAssistantVisualStyle.PixelAnimated ? "Pixel" : "Chibi";
        return $"Assets/Hanabe/Animated/{folder}/{state.ToString().ToLowerInvariant()}.gif";
    }
}
