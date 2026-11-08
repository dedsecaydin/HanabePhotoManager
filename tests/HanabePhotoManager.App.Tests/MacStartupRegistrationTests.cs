using System.IO;
using System.Xml.Linq;
using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacStartupRegistrationTests
{
    [Fact]
    public void EnableAndDisable_OnlyChangeTheHanabeLaunchAgent()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "mac-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var agent = Path.Combine(root, "com.hanabe.photomanager.plist");
            var other = Path.Combine(root, "other.plist");
            File.WriteAllText(other, "untouched");
            var startup = new MacStartupRegistration(agent, "/Applications/Hanabe.app/Contents/MacOS/Hanabe");
            Assert.False(startup.IsEnabled());
            startup.SetEnabled(true);
            Assert.True(startup.IsEnabled());
            Assert.Contains(XDocument.Load(agent).Descendants("string"), value => value.Value == "/Applications/Hanabe.app/Contents/MacOS/Hanabe");
            startup.SetEnabled(false);
            Assert.False(startup.IsEnabled());
            Assert.Equal("untouched", File.ReadAllText(other));
        }
        finally { Directory.Delete(root, true); }
    }
}
