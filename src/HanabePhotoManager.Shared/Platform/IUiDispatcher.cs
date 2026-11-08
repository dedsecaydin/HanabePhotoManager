namespace HanabePhotoManager.App.Platform;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action, bool background = false);
    Task InvokeAsync(Func<Task> work);
    Task<T> InvokeAsync<T>(Func<T> work);
}

public static class UiDispatcherExtensions
{
    public static Task InvokeAsync(this IUiDispatcher dispatcher, Action action) =>
        dispatcher.InvokeAsync(() => { action(); return 0; });
}

/// <summary>A synchronous dispatcher for tests and non-UI contexts.</summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;
    public void Post(Action action, bool background = false) => action();
    public Task InvokeAsync(Func<Task> work) => work();
    public Task<T> InvokeAsync<T>(Func<T> work) => Task.FromResult(work());
}
