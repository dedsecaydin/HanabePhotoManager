namespace HanabePhotoManager.Core.Cloud;

public sealed record CloudSchedulingState(
    bool ImportRunning,
    bool QuarkRunning,
    bool HighResolutionPreviewRunning,
    bool NetworkBusy,
    bool BaiduCapacityAvailable,
    bool BaiduAuthenticated);

public static class CloudSchedulingPolicy
{
    public static bool CanRunBaidu(CloudSchedulingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return
            !state.ImportRunning &&
            !state.QuarkRunning &&
            !state.HighResolutionPreviewRunning &&
            !state.NetworkBusy &&
            state.BaiduCapacityAvailable &&
            state.BaiduAuthenticated;
    }
}
