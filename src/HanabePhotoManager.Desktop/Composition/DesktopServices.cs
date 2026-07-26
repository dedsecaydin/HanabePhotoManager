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

public static class DesktopComposition
{
    public static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddHanabeDesktop()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public static DesktopShellViewModel ResolveServicesForCurrentPlatform(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var shellViewModel = serviceProvider.GetRequiredService<DesktopShellViewModel>();

        foreach (var serviceType in DesktopServiceResolutionPolicy.GetPlatformServiceTypes(OperatingSystem.IsMacOS()))
        {
            _ = serviceProvider.GetRequiredService(serviceType);
        }

        return shellViewModel;
    }
}

public static class DesktopServiceResolutionPolicy
{
    public static IReadOnlyList<Type> GetPlatformServiceTypes(bool isMacOs)
    {
        return isMacOs
            ? [
                typeof(IAppPaths),
                typeof(ITrashService),
                typeof(IExternalFileService),
                typeof(IProcessRunner)
            ]
            : Array.Empty<Type>();
    }
}
