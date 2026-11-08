using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MapTileCacheTests
{
    [Fact]
    public async Task ServerAgeReducesRemainingCacheLifetime()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "tiles-age-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var handler = new AgedHandler();
            using var client = new HttpClient(handler);
            var cache = new MapTileCache(root, client);
            await cache.GetTileAsync(1, 0, 0, default);
            await cache.GetTileAsync(1, 0, 0, default);
            Assert.Equal(2, handler.Calls);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExpiredTileUsesConditionalRequestAndReusesUnchangedData()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "tiles-revalidate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var handler = new RevalidatingHandler();
            using var client = new HttpClient(handler);
            var cache = new MapTileCache(root, client);
            var first = await cache.GetTileAsync(1, 0, 0, default);
            var second = await cache.GetTileAsync(1, 0, 0, default);
            var third = await cache.GetTileAsync(1, 0, 0, default);
            Assert.Equal(first, second);
            Assert.Equal(second, third);
            Assert.Equal(2, handler.Calls);
            Assert.True(handler.SawETag);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RepeatedVisibleTileUsesCacheAndIdentifiesApplication()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "tiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var handler = new TileHandler();
            using var client = new HttpClient(handler);
            var cache = new MapTileCache(root, client);
            var first = await cache.GetTileAsync(2, 1, 3, default);
            var second = await cache.GetTileAsync(2, 1, 3, default);
            Assert.Equal(first, second);
            Assert.Equal(1, handler.Calls);
            Assert.Contains("HanabePhotoManager", handler.UserAgent);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.GetTileAsync(2, 4, 0, default));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class TileHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string UserAgent { get; private set; } = string.Empty;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; UserAgent = request.Headers.UserAgent.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]) };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(7) };
            return Task.FromResult(response);
        }
    }

    private sealed class RevalidatingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool SawETag { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            SawETag |= request.Headers.IfNoneMatch.Any(tag => tag.Tag == "\"tile-v1\"");
            var response = Calls == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]) }
                : new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"tile-v1\"");
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = Calls == 1 ? TimeSpan.Zero : TimeSpan.FromDays(7) };
            return Task.FromResult(response);
        }
    }

    private sealed class AgedHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]) };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(7) };
            response.Headers.Age = TimeSpan.FromDays(7);
            return Task.FromResult(response);
        }
    }
}
