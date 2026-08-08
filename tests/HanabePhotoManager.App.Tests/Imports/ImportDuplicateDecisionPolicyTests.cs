using FluentAssertions;
using HanabePhotoManager.App.Duplicates;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

public sealed class ImportDuplicateDecisionPolicyTests
{
    [Theory]
    [InlineData(ImportDuplicateDecision.Skip, false)]
    [InlineData(ImportDuplicateDecision.ImportAnyway, true)]
    [InlineData(ImportDuplicateDecision.LocateExisting, false)]
    public void ShouldTransfer_UsesTheExplicitDuplicateDecision(
        ImportDuplicateDecision decision,
        bool expected)
    {
        ImportDuplicateDecisionPolicy.ShouldTransfer(decision).Should().Be(expected);
    }
}
