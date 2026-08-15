using FluentAssertions;
using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Core.Tests.Platform;

public sealed class PlatformContractTests
{
    [Fact]
    public void TrashService_ExposesCancelableAsyncOperation()
    {
        var method = typeof(ITrashService).GetMethod(nameof(ITrashService.MoveToTrashAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task>();
        method.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(string), typeof(CancellationToken));
        method.GetParameters()[1].IsOptional.Should().BeTrue();
    }

    [Fact]
    public void AppPaths_SeparatesDurableAndCachedData()
    {
        typeof(IAppPaths).GetProperty(nameof(IAppPaths.ApplicationDataDirectory)).Should().NotBeNull();
        typeof(IAppPaths).GetProperty(nameof(IAppPaths.CacheDirectory)).Should().NotBeNull();
    }

    [Fact]
    public void ExternalFileService_ExposesCancelableAsyncRevealOperation()
    {
        var method = typeof(IExternalFileService).GetMethod(nameof(IExternalFileService.RevealInFileManagerAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task>();
        method.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(string), typeof(CancellationToken));
        method.GetParameters()[1].IsOptional.Should().BeTrue();
    }
}
