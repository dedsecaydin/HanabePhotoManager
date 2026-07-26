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
    public static async Task<int> RunAsync(IProcessHandle process, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        var standardErrorTask = process.ReadStandardErrorAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateAndDrainAsync(process, standardErrorTask);
            throw;
        }

        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new ProcessExitException(process.ExitCode, standardError);
        }

        return process.ExitCode;
    }

    private static async Task TerminateAndDrainAsync(IProcessHandle process, Task<string> standardErrorTask)
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process can exit after HasExited is checked but before Kill is called.
                }
                catch
                {
                    // Cleanup failures must not replace the caller's cancellation.
                }
            }
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }

        try
        {
            await standardErrorTask;
        }
        catch
        {
            // Cleanup failures must not replace the caller's cancellation.
        }
    }
}
