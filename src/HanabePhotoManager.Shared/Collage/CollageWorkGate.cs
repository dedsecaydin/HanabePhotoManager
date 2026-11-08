namespace HanabePhotoManager.App.Collage;
/// <summary>One collage allocation or preview decode at a time across windows; queued work remains cancellable.</summary>
internal static class CollageWorkGate
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    public static async Task<IDisposable> EnterAsync(CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);return new Lease();
    }
    private sealed class Lease:IDisposable
    {
        private int disposed;
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)Gate.Release();}
    }
}
