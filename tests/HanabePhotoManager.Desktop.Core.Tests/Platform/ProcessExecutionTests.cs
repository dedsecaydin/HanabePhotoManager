using FluentAssertions;
using HanabePhotoManager.Desktop.Platform;

namespace HanabePhotoManager.Desktop.Core.Tests.Platform;

public sealed class ProcessExecutionTests
{
    [Fact]
    public async Task RunAsync_CancellationKillsEntireProcessTreeWaitsForExitAndDrainsStandardError()
    {
        var process = new ControlledProcess();
        using var cancellationSource = new CancellationTokenSource();

        var running = ProcessExecution.RunAsync(process, cancellationSource.Token);
        await process.CancellableWaitStarted.Task;
        cancellationSource.Cancel();

        var action = async () => await running;

        await action.Should().ThrowAsync<OperationCanceledException>();
        process.KillEntireProcessTree.Should().BeTrue();
        process.WaitCancellationTokens.Should().ContainSingle(token => !token.CanBeCanceled);
        process.StandardErrorReadCount.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_CancellationIsPreservedWhenProcessCleanupFails()
    {
        var process = new ControlledProcess { ThrowWhenKilled = true };
        using var cancellationSource = new CancellationTokenSource();

        var running = ProcessExecution.RunAsync(process, cancellationSource.Token);
        await process.CancellableWaitStarted.Task;
        cancellationSource.Cancel();

        var action = async () => await running;

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_CancellationHandlesProcessExitRaceBeforeKill()
    {
        var process = new ControlledProcess { ExitWhenKilled = true };
        using var cancellationSource = new CancellationTokenSource();

        var running = ProcessExecution.RunAsync(process, cancellationSource.Token);
        await process.CancellableWaitStarted.Task;
        cancellationSource.Cancel();

        var action = async () => await running;

        await action.Should().ThrowAsync<OperationCanceledException>();
        process.WaitCancellationTokens.Should().ContainSingle(token => !token.CanBeCanceled);
        process.StandardErrorReadCount.Should().Be(1);
    }

    private sealed class ControlledProcess : IProcessHandle
    {
        public TaskCompletionSource CancellableWaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<CancellationToken> WaitCancellationTokens { get; } = [];

        public bool HasExited { get; private set; }

        public bool KillEntireProcessTree { get; private set; }

        public bool ThrowWhenKilled { get; init; }

        public bool ExitWhenKilled { get; init; }

        public int StandardErrorReadCount { get; private set; }

        public int ExitCode => 0;

        public void Kill(bool entireProcessTree)
        {
            KillEntireProcessTree = entireProcessTree;

            if (ThrowWhenKilled)
            {
                throw new InvalidOperationException("Simulated cleanup failure.");
            }

            if (ExitWhenKilled)
            {
                HasExited = true;
                throw new InvalidOperationException("The process exited before it could be killed.");
            }

            HasExited = true;
        }

        public Task<string> ReadStandardErrorAsync()
        {
            StandardErrorReadCount++;
            return Task.FromResult("simulated standard error");
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitCancellationTokens.Add(cancellationToken);

            if (!cancellationToken.CanBeCanceled)
            {
                HasExited = true;
                return Task.CompletedTask;
            }

            CancellableWaitStarted.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
