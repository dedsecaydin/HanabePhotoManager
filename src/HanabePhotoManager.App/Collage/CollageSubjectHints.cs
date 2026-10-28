using HanabePhotoManager.Core.Collage;
using HanabePhotoManager.App.Services;
namespace HanabePhotoManager.App.Collage;
public sealed class CollageSubjectHints(ILocalFaceEmbeddingService? detector = null)
{
    private readonly ILocalFaceEmbeddingService faces = detector ?? new LocalFaceEmbeddingService();
    public async Task<(IReadOnlyList<CollagePlan> Plans, string Message)> ApplyAsync(IReadOnlyList<CollagePlan> plans, IReadOnlyList<CollagePhoto> photos, CancellationToken token)
    {
        var focus = new Dictionary<string, (double X, double Y)>(); int failed = 0;
        foreach (var photo in photos)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var work=await CollageWorkGate.EnterAsync(token).ConfigureAwait(false);
                var detected = await faces.DetectAsync(photo.Id, token).ConfigureAwait(false);
                if (detected.Count == 0) continue;
                double left = detected.Min(f => f.X), top = detected.Min(f => f.Y), right = detected.Max(f => f.X + f.Width), bottom = detected.Max(f => f.Y + f.Height);
                focus[photo.Id] = (Math.Clamp((left + right) / (2 * photo.Width), 0, 1), Math.Clamp((top + bottom) / (2 * photo.Height), 0, 1));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { failed++; }
        }
        return (plans.Select(p => p with { Slots = p.Slots.Select(s => focus.TryGetValue(s.Id, out var f) ? s with { FocusX = f.X, FocusY = f.Y } : s).ToArray() }).ToArray(), $"人脸建议用于 {focus.Count} 张；无脸或检测失败采用中心裁切（失败 {failed} 张）。请检查预览，无法保证所有主体完整。 ");
    }
}

