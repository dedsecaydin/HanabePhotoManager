using FluentAssertions;
using HanabePhotoManager.App.Duplicates;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DuplicateMergeScopePolicyTests
{
    [Theory]
    [InlineData(DuplicateMergeScope.ExactSha256Only, true, false)]
    [InlineData(DuplicateMergeScope.VisualSimilarOnly, false, true)]
    [InlineData(DuplicateMergeScope.ExactAndVisual, true, true)]
    public void Scope_SelectsExpectedAlgorithms(DuplicateMergeScope scope, bool exact, bool visual)
    {
        DuplicateMergeScopePolicy.IncludesExact(scope).Should().Be(exact);
        DuplicateMergeScopePolicy.IncludesVisual(scope).Should().Be(visual);
    }
}
