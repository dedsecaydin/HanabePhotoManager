using System.Reflection;
using FluentAssertions;
using HanabePhotoManager.Desktop;

namespace HanabePhotoManager.Desktop.Core.Tests;

public sealed class ProgramSmokeTests
{
    [Fact]
    public void RunSmokeTest_LoadsTheAvaloniaApplicationAndMainWindowWithoutShowing()
    {
        var programType = typeof(App).Assembly.GetType("HanabePhotoManager.Desktop.Program");

        programType.Should().NotBeNull();

        var method = programType!.GetMethod("RunSmokeTest", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var result = method!.Invoke(null, null);

        result.Should().Be(0);
    }
}
