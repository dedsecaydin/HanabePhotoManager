using System.Collections.Specialized;

namespace HanabePhotoManager.App.WeChat;

public interface IWeChatFileClipboard
{
    void SetFiles(IReadOnlyList<string> files);
}

public sealed class WindowsWeChatFileClipboard : IWeChatFileClipboard
{
    public void SetFiles(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("至少需要一个文件。", nameof(files));
        }

        var dropList = new StringCollection();
        dropList.AddRange(files.ToArray());
        System.Windows.Clipboard.SetFileDropList(dropList);
    }
}
