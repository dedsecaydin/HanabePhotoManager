using Microsoft.Win32;

namespace HanabePhotoManager.App.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public sealed class WindowsStartupRegistrationService : IStartupRegistrationService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HanabePhotoManager";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows 当前用户启动项。");
        if (enabled)
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath)) throw new InvalidOperationException("无法确定应用程序路径。");
            key.SetValue(ValueName, $"\"{processPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
