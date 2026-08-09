using FluentAssertions;
using HanabePhotoManager.App.Duplicates;
using Xunit;

namespace HanabePhotoManager.App.Tests.Imports;

public sealed class ImportDuplicateBatchDecisionPolicyTests
{
    [Theory]
    [InlineData(ImportDuplicateBatchDecision.SkipAll, false)]
    [InlineData(ImportDuplicateBatchDecision.ImportAll, true)]
    [InlineData(ImportDuplicateBatchDecision.DecideIndividually, false)]
    public void ShouldTransfer_OnlyImportsWhenUserChoosesImportAll(ImportDuplicateBatchDecision decision, bool expected)
    {
        ImportDuplicateBatchDecisionPolicy.ShouldTransfer(decision).Should().Be(expected);
    }

    [Fact]
    public void ShouldPromptIndividually_OnlyForExplicitPerItemChoice()
    {
        ImportDuplicateBatchDecisionPolicy.ShouldPromptIndividually(ImportDuplicateBatchDecision.DecideIndividually).Should().BeTrue();
        ImportDuplicateBatchDecisionPolicy.ShouldPromptIndividually(ImportDuplicateBatchDecision.SkipAll).Should().BeFalse();
    }
}
