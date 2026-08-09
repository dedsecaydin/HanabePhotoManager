using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class ImportProgressTests
{
    [Fact]
    public void Complete_TracksCompletedUnitsAndCapsAtTotal()
    {
        var progress = ImportProgress.Create(3).Complete(1).Complete(10);

        progress.CompletedUnits.Should().Be(3);
        progress.TotalUnits.Should().Be(3);
        progress.Percentage.Should().Be(100);
    }

    [Fact]
    public void Create_EmptyBatchIsComplete()
    {
        ImportProgress.Create(0).Percentage.Should().Be(100);
    }

    [Fact]
    public void Cancel_PreservesCountsAndMarksTerminalState()
    {
        var progress = ImportProgress.Create(4).Complete(2).Cancel();

        progress.IsCanceled.Should().BeTrue();
        progress.CompletedUnits.Should().Be(2);
    }
}
