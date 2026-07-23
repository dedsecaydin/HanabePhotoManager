using System.Threading;
using System.Windows;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RemarkPromptWindowTests
{
    [Fact]
    public void Window_SizesItsHeightToContent_SoActionButtonsAreNotClipped()
    {
        Exception? failure = null;
        SizeToContent? sizeMode = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new RemarkPromptWindow("07.16");
                sizeMode = window.SizeToContent;
                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.Should().BeNull();
        sizeMode.Should().Be(SizeToContent.Height);
    }
}
