using HanabePhotoManager.Core.Collage;
using HanabePhotoManager.App.Services;
namespace HanabePhotoManager.App.Collage;
public sealed record CollageHeroSuggestion(string PhotoId,double Score,string Reason);
public sealed class CollageHeroSuggester(ILocalFaceEmbeddingService? detector=null)
{
    private readonly ILocalFaceEmbeddingService faces=detector??new LocalFaceEmbeddingService();
    public async Task<CollageHeroSuggestion> SuggestAsync(IReadOnlyList<CollagePhoto> photos,CancellationToken token)
    {
        if(photos.Count==0)throw new ArgumentException("请先添加照片。");
        var scores=new List<CollageHeroSuggestion>();
        foreach(var photo in photos)
        {
            token.ThrowIfCancellationRequested();
            double pixels=(long)photo.Width*photo.Height;
            double geometry=.3*Math.Min(1,Math.Log10(Math.Max(1,pixels))/8)+.2/(1+Math.Abs(Math.Log((double)photo.Width/photo.Height/1.5)));
            double faceScore=0;bool modelUsed=false;
            try
            {
                using var gate=await CollageWorkGate.EnterAsync(token).ConfigureAwait(false);
                var detected=await faces.DetectAsync(photo.Id,token).ConfigureAwait(false);modelUsed=true;
                faceScore=detected.Count==0?0:detected.Max(f=>.5*Math.Clamp(f.Confidence,0,1)*Math.Min(1,Math.Sqrt((double)f.Width*f.Height/pixels)*3));
            }
            catch(OperationCanceledException){throw;}
            catch(Exception){}
            scores.Add(new(photo.Id,geometry+faceScore,modelUsed?"本地人脸置信度/占比 + 分辨率/构图比例评分；请人工检查表情和清晰度。":"模型不可用，已回退到分辨率/构图比例评分，未使用人脸分析。"));
        }
        return scores.OrderByDescending(s=>s.Score).First(); // Stable input order resolves equal scores.
    }
}
