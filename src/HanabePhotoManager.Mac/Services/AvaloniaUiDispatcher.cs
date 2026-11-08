using Avalonia.Threading;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.Mac.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
    public void Post(Action action, bool background = false) =>
        Dispatcher.UIThread.Post(action, background ? DispatcherPriority.Background : DispatcherPriority.Normal);
    public async Task InvokeAsync(Func<Task> work) => await Dispatcher.UIThread.InvokeAsync(work);
    public async Task<T> InvokeAsync<T>(Func<T> work) => await Dispatcher.UIThread.InvokeAsync(work);
}
