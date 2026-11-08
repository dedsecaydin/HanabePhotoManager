using HanabePhotoManager.App.Platform;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class PlatformDispatcherTests
{
    [Fact]
    public async Task InlineDispatcherExecutesWorkAndPropagatesResults()
    {
        IUiDispatcher dispatcher = new InlineUiDispatcher();
        var called = false;
        dispatcher.Post(() => called = true);
        Assert.True(called);
        Assert.True(dispatcher.CheckAccess());
        Assert.Equal(42, await dispatcher.InvokeAsync(() => 42));
        await dispatcher.InvokeAsync(() => Task.CompletedTask);
    }

    [Fact]
    public async Task WpfDispatcherRunsWorkAndReturnsResults()
    {
        IUiDispatcher dispatcher = new WpfUiDispatcher(System.Windows.Threading.Dispatcher.CurrentDispatcher);
        Assert.True(dispatcher.CheckAccess());
        Assert.Equal(42, await dispatcher.InvokeAsync(() => 42));
        await dispatcher.InvokeAsync(() => Task.CompletedTask);
    }

    [Fact]
    public async Task DispatcherActionCanBeAwaited()
    {
        IUiDispatcher dispatcher = new InlineUiDispatcher();
        var called = false;
        await dispatcher.InvokeAsync((Action)(() => called = true));
        Assert.True(called);
    }
}
