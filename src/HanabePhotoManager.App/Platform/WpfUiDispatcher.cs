using System.Windows.Threading;

namespace HanabePhotoManager.App.Platform;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public bool CheckAccess() => dispatcher.CheckAccess();
    public void Post(Action action, bool background = false) =>
        dispatcher.BeginInvoke(action, background ? DispatcherPriority.Background : DispatcherPriority.Normal);
    public Task InvokeAsync(Func<Task> work) => dispatcher.CheckAccess()
        ? work() : dispatcher.InvokeAsync(work).Task.Unwrap();
    public Task<T> InvokeAsync<T>(Func<T> work) => dispatcher.CheckAccess()
        ? Task.FromResult(work()) : dispatcher.InvokeAsync(work).Task;
}
