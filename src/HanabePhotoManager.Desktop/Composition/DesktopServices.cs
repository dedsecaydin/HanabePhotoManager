using HanabePhotoManager.Desktop.Core.Platform;
using HanabePhotoManager.Desktop.Core.ViewModels;
using HanabePhotoManager.Desktop.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace HanabePhotoManager.Desktop.Composition;

public static class DesktopServices
{
    public static IServiceCollection AddHanabeDesktop(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddSingleton<IProcessRunner, ProcessRunner>()
            .AddSingleton<IAppPaths, MacOsAppPaths>()
            .AddSingleton<ITrashService, MacOsTrashService>()
            .AddSingleton<IExternalFileService, MacOsExternalFileService>()
            .AddSingleton<DesktopShellViewModel>();
    }
}
