using FluentAssertions;
using HanabePhotoManager.Desktop.Composition;
using HanabePhotoManager.Desktop.Core.Platform;
using HanabePhotoManager.Desktop.Core.ViewModels;
using HanabePhotoManager.Desktop.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace HanabePhotoManager.Desktop.Core.Tests.Composition;

public sealed class ServiceContractTests
{
    [Fact]
    public void GetPlatformServiceTypes_WhenNotRunningOnMacOs_ReturnsNoMacOsOnlyServices()
    {
        DesktopServiceResolutionPolicy.GetPlatformServiceTypes(isMacOs: false)
            .Should().BeEmpty();
    }

    [Fact]
    public void GetPlatformServiceTypes_WhenRunningOnMacOs_ReturnsEveryMacOsOnlyService()
    {
        DesktopServiceResolutionPolicy.GetPlatformServiceTypes(isMacOs: true)
            .Should().Equal(
                typeof(IAppPaths),
                typeof(ITrashService),
                typeof(IExternalFileService),
                typeof(IProcessRunner));
    }

    [Fact]
    public void AddHanabeDesktop_RegistersOneImplementationPerPlatformContract()
    {
        var services = new ServiceCollection()
            .AddHanabeDesktop();

        AssertSingletonRegistration<IProcessRunner, ProcessRunner>(services);
        AssertSingletonRegistration<IAppPaths, MacOsAppPaths>(services);
        AssertSingletonRegistration<ITrashService, MacOsTrashService>(services);
        AssertSingletonRegistration<IExternalFileService, MacOsExternalFileService>(services);
        AssertSingletonRegistration<DesktopShellViewModel, DesktopShellViewModel>(services);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        provider.GetRequiredService<DesktopShellViewModel>().Should().NotBeNull();
    }

    private static void AssertSingletonRegistration<TService, TImplementation>(IServiceCollection services)
    {
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(TService))
            .ToList();

        registrations.Should().ContainSingle();
        registrations[0].ImplementationType.Should().Be(typeof(TImplementation));
        registrations[0].Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
