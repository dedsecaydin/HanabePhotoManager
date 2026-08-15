using System.Text.RegularExpressions;
using FluentAssertions;

namespace HanabePhotoManager.Desktop.Core.Tests.Packaging;

public sealed class MacOsWorkflowTests
{
    [Fact]
    public void Workflow_BuildsAndUploadsArm64App()
    {
        var workflowPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "macos-arm64.yml");

        File.Exists(workflowPath).Should().BeTrue("the authoritative macOS ARM64 workflow must exist");

        var workflow = File.ReadAllText(workflowPath);
        var commands = Regex.Replace(workflow, @"\\\r?\n\s*", " ");
        commands = Regex.Replace(commands, @"[ \t]+", " ");

        AssertContains(workflow, @"runs-on:\s*macos-14");
        AssertContains(commands, @"dotnet\s+test\s+tests/HanabePhotoManager\.Core\.Tests/HanabePhotoManager\.Core\.Tests\.csproj");
        AssertContains(commands, @"dotnet\s+test\s+tests/HanabePhotoManager\.Desktop\.Core\.Tests/HanabePhotoManager\.Desktop\.Core\.Tests\.csproj");
        AssertContains(commands, @"dotnet\s+publish\s+src/HanabePhotoManager\.Desktop/HanabePhotoManager\.Desktop\.csproj");
        AssertContains(commands, @"-r\s+osx-arm64");
        AssertContains(commands, @"--self-contained\s+true");
        AssertContains(commands, @"create-app-bundle\.sh");
        AssertContains(
            commands,
            @"""artifacts/macos/bundle/Hanabe Photo Manager\.app/Contents/MacOS/HanabePhotoManager\.Desktop""\s+--smoke-test");
        commands.Should().NotMatchRegex(
            @"artifacts/macos/publish/HanabePhotoManager\.Desktop\s+--smoke-test",
            "the smoke test must exercise the executable inside the generated app bundle");
        AssertContains(commands, @"ditto\s+-c\s+-k");
        AssertContains(commands, @"shasum\s+-a\s+256");
        AssertContains(workflow, @"actions/upload-artifact@v\d+");

        workflow.Should().NotContain(
            "HanabePhotoManager.App.Tests",
            "the WPF test project cannot run on macOS");
        workflow.Should().NotContain(
            "HanabePhotoManager.Infrastructure.Tests",
            "phase 1 Infrastructure still contains Windows-only DPAPI, kernel32, and lock semantics");
    }

    private static void AssertContains(string workflow, string pattern)
    {
        Regex.IsMatch(workflow, pattern, RegexOptions.CultureInvariant)
            .Should()
            .BeTrue($"the workflow should match /{pattern}/");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the test output directory.");
    }
}
