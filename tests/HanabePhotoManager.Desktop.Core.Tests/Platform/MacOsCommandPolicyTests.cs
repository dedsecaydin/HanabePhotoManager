using FluentAssertions;
using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Core.Tests.Platform;

public sealed class MacOsCommandPolicyTests
{
    [Fact]
    public void Trash_UsesFinderDeleteWithoutShellInterpolation()
    {
        var command = MacOsCommandPolicy.MoveToTrash("/Users/me/Pictures/a 'quoted'.jpg");

        command.FileName.Should().Be("/usr/bin/osascript");
        command.Arguments.Should().Equal(
            "-e",
            "on run argv",
            "-e",
            "tell application \"Finder\" to delete POSIX file (item 1 of argv)",
            "-e",
            "end run",
            "--",
            "/Users/me/Pictures/a 'quoted'.jpg");
    }

    [Fact]
    public void Reveal_UsesOpenRevealWithSeparateArgument()
    {
        var command = MacOsCommandPolicy.Reveal("/Users/me/Pictures/a b.jpg");

        command.FileName.Should().Be("/usr/bin/open");
        command.Arguments.Should().Equal("-R", "/Users/me/Pictures/a b.jpg");
    }
}
