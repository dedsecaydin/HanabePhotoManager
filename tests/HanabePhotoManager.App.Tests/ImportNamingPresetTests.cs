using FluentAssertions;
using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportNamingPresetTests
{
    [Theory]
    [InlineData("JK{seq}", ImportNamingPresetKind.Sequence)]
    [InlineData("{orig}", ImportNamingPresetKind.Original)]
    [InlineData("JK{seq}（{orig}）", ImportNamingPresetKind.SequenceAndOriginal)]
    [InlineData("{date}_{orig}", ImportNamingPresetKind.Custom)]
    public void Resolve_MapsKnownTemplatesAndPreservesCustom(string template, ImportNamingPresetKind expected)
    {
        ImportNamingPreset.Resolve(template).Kind.Should().Be(expected);
    }
}
