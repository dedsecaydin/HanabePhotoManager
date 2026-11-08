using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public partial class MainWindowViewModel
{
    private bool _soundStart = true, _soundCompletion = true, _soundFailure = true, _soundSkip = true, _soundOpen, _soundCancel = true;
    public bool HanabeSoundStartEnabled { get => _soundStart; set { if (SetProperty(ref _soundStart, value)) _ = SaveSettingsAsync(); } }
    public bool HanabeSoundCompletionEnabled { get => _soundCompletion; set { if (SetProperty(ref _soundCompletion, value)) _ = SaveSettingsAsync(); } }
    public bool HanabeSoundFailureEnabled { get => _soundFailure; set { if (SetProperty(ref _soundFailure, value)) _ = SaveSettingsAsync(); } }
    public bool HanabeSoundSkipEnabled { get => _soundSkip; set { if (SetProperty(ref _soundSkip, value)) _ = SaveSettingsAsync(); } }
    public bool HanabeSoundOpenEnabled { get => _soundOpen; set { if (SetProperty(ref _soundOpen, value)) _ = SaveSettingsAsync(); } }
    public bool HanabeSoundCancelEnabled { get => _soundCancel; set { if (SetProperty(ref _soundCancel, value)) _ = SaveSettingsAsync(); } }

    [RelayCommand]
    private void PreviewSoundEvent(string? eventName)
    {
        if (Enum.TryParse<HanabeSoundEvent>(eventName, out var kind))
            _hanabeSoundService.PreviewEvent(kind, HanabeSoundStyle, HanabeSoundVolume);
    }

    private void LoadSoundEvents(AppSettings settings)
    {
        HanabeSoundStartEnabled = settings.HanabeSoundStartEnabled;
        HanabeSoundCompletionEnabled = settings.HanabeSoundCompletionEnabled;
        HanabeSoundFailureEnabled = settings.HanabeSoundFailureEnabled;
        HanabeSoundSkipEnabled = settings.HanabeSoundSkipEnabled;
        HanabeSoundOpenEnabled = settings.HanabeSoundOpenEnabled;
        HanabeSoundCancelEnabled = settings.HanabeSoundCancelEnabled;
    }

    private void SaveSoundEvents(AppSettings settings)
    {
        settings.HanabeSoundStartEnabled = HanabeSoundStartEnabled;
        settings.HanabeSoundCompletionEnabled = HanabeSoundCompletionEnabled;
        settings.HanabeSoundFailureEnabled = HanabeSoundFailureEnabled;
        settings.HanabeSoundSkipEnabled = HanabeSoundSkipEnabled;
        settings.HanabeSoundOpenEnabled = HanabeSoundOpenEnabled;
        settings.HanabeSoundCancelEnabled = HanabeSoundCancelEnabled;
    }
}
