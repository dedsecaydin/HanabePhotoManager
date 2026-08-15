using System.Diagnostics;

namespace HanabePhotoManager.Desktop.Platform;

internal interface IProcessHandle
{
    bool HasExited { get; }

    int ExitCode { get; }

    void Kill(bool entireProcessTree);

    Task<string> ReadStandardErrorAsync();

    Task WaitForExitAsync(CancellationToken cancellationToken);
}

internal sealed class ProcessHandle : IProcessHandle
{
    private readonly Process _process;

    public ProcessHandle(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public void Kill(bool entireProcessTree) => _process.Kill(entireProcessTree);

    public Task<string> ReadStandardErrorAsync() => _process.StandardError.ReadToEndAsync();

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);
}

internal sealed class ProcessExitException : InvalidOperationException
{
    public ProcessExitException(int exitCode, string standardError)
        : base($"Process exited with code {exitCode}: {standardError}")
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardError { get; }
}

internal static class ProcessExecution
{
    internal static readonly TimeSpan CancellationCleanupTimeout = TimeSpan.FromSeconds(2);

    public static Task<int> RunAsync(IProcessHandle process, CancellationToken cancellationToken) =>
        RunAsync(process, cancellationToken, CancellationCleanupTimeout);

    public static async Task<int> RunAsync(
        IProcessHandle process,
        CancellationToken cancellationToken,
        TimeSpan cleanupTimeout)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (cleanupTimeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cleanupTimeout));
        }

        var standardErrorTask = process.ReadStandardErrorAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateAndDrainAsync(process, standardErrorTask, cleanupTimeout);
            throw;
        }

        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new ProcessExitException(process.ExitCode, standardError);
        }

        return process.ExitCode;
    }

    private static async Task TerminateAndDrainAsync(
        IProcessHandle process,
        Task<string> standardErrorTask,
        TimeSpan cleanupTimeout)
    {
        TryTerminate(process);

        await ObserveWithinCleanupTimeoutAsync(
            () => process.WaitForExitAsync(CancellationToken.None),
            cleanupTimeout);
        await ObserveWithinCleanupTimeoutAsync(standardErrorTask, cleanupTimeout);
    }

    private static void TryTerminate(IProcessHandle process)
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    TryTerminateDirectly(process);
                }
            }
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }
    }

    private static void TryTerminateDirectly(IProcessHandle process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: false);
            }
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }
    }

    private static async Task ObserveWithinCleanupTimeoutAsync(Func<Task> taskFactory, TimeSpan cleanupTimeout)
    {
        try
        {
            await ObserveWithinCleanupTimeoutAsync(taskFactory(), cleanupTimeout);
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }
    }

    private static async Task ObserveWithinCleanupTimeoutAsync(Task task, TimeSpan cleanupTimeout)
    {
        if (task.IsCompleted)
        {
            await ObserveCompletedTaskAsync(task);
            return;
        }

        await Task.WhenAny(task, Task.Delay(cleanupTimeout));

        if (task.IsCompleted)
        {
            await ObserveCompletedTaskAsync(task);
            return;
        }

        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task ObserveCompletedTaskAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }
    }
}
