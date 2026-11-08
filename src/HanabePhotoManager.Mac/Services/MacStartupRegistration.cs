using System.IO;
using System.Xml.Linq;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.Mac.Services;

internal sealed class MacStartupRegistration(string plistPath, string executable) : IStartupRegistrationService
{
    public bool IsEnabled() => File.Exists(plistPath);

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(plistPath)) File.Delete(plistPath);
            return;
        }

        if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException("无法确定程序启动路径。");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(plistPath))!);
        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"), new XElement("dict",
                new XElement("key", "Label"), new XElement("string", "com.hanabe.photomanager"),
                new XElement("key", "ProgramArguments"), new XElement("array", new XElement("string", executable)),
                new XElement("key", "RunAtLoad"), new XElement("true"))));
        var temporary = plistPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { document.Save(temporary); File.Move(temporary, plistPath, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
