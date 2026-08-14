using System.IO;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.App.Cloud;

/// <summary>
/// View-model projection of one persisted <see cref="CloudTransferJob"/> for
/// the cloud page transfer queue. Jobs carry no transfer direction, so the
/// row shows the first file's local name, the remote destination, the job
/// state and an aggregate progress — never fabricated direction metadata.
/// </summary>
public sealed class CloudTransferJobItemViewModel
{
    public CloudTransferJobItemViewModel(CloudTransferJob job)
    {
        Job = job ?? throw new ArgumentNullException(nameof(job));
    }

    public CloudTransferJob Job { get; }

    public string Title
    {
        get
        {
            var first = Job.Files[0];
            var name = Path.GetFileName(first.LocalPath);
            return string.IsNullOrWhiteSpace(name) ? first.RelativePath.Value : name;
        }
    }

    public string Subtitle => $"目标 {Job.Destination.Value} · {Job.Files.Count} 个文件";

    public string StateText => Job.State switch
    {
        CloudTransferState.Pending => "排队中",
        CloudTransferState.Running => "传输中",
        CloudTransferState.Paused => "已暂停",
        CloudTransferState.Verifying => "校验中",
        CloudTransferState.Completed => "已完成",
        CloudTransferState.Failed => "失败",
        CloudTransferState.Canceled => "已取消",
        _ => Job.State.ToString()
    };

    public double Progress
    {
        get
        {
            long uploaded = 0;
            long size = 0;
            foreach (var file in Job.Files)
            {
                uploaded += file.UploadedBytes;
                size += file.Size;
            }

            return size <= 0 ? 0 : Math.Clamp(uploaded / (double)size, 0, 1);
        }
    }

    public bool IsActive => Job.State is
        CloudTransferState.Running or
        CloudTransferState.Pending or
        CloudTransferState.Verifying;
}
