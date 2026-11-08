using System.IO;
using System.Xml.Linq;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacEntitlementsTests
{
    [Fact]
    public void MacHardenedRuntimeIncludesDotnetEntitlements()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "HanabePhotoManager.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src", "HanabePhotoManager.Mac", "Packaging", "Entitlements.plist"));
        var entries = document.Root!.Element("dict")!.Elements().ToArray();
        foreach (var name in new[] { "com.apple.security.cs.allow-jit", "com.apple.security.cs.allow-unsigned-executable-memory", "com.apple.security.cs.allow-dyld-environment-variables", "com.apple.security.cs.disable-library-validation" })
        {
            var index = Array.FindIndex(entries, element => element.Name == "key" && element.Value == name);
            Assert.True(index >= 0 && index + 1 < entries.Length && entries[index + 1].Name == "true", name);
        }
    }
}
