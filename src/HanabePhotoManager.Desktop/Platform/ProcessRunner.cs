using System.Diagnostics;
using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Platform;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(ProcessCommand command, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("ProcessRunner can only be used on macOS.");
        }

        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FileName);
        ArgumentNullException.ThrowIfNull(command.Arguments);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            RedirectStandardError = true
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start process '{command.FileName}'.");

        try
        {
            return await ProcessExecution.RunAsync(new ProcessHandle(process), cancellationToken);
        }
        catch (ProcessExitException exception)
        {
            throw new InvalidOperationException(
                $"Process '{command.FileName}' exited with code {exception.ExitCode}: {exception.StandardError}",
                exception);
        }
    }
}
