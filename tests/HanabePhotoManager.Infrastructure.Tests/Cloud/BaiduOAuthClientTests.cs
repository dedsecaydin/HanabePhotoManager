using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class BaiduOAuthClientTests
{
    [Fact]
    public void BuildAuthorizeUri_UsesOfficialOobParameters()
    {
        var client = new BaiduOAuthClient(new HttpClient());
        var uri = client.BuildAuthorizeUri("app id", "state-1");
        uri.Host.Should().Be("openapi.baidu.com");
        uri.AbsolutePath.Should().Be("/oauth/2.0/authorize");
        uri.Query.Should().Contain("response_type=code");
        uri.Query.Should().Contain("client_id=app%20id");
        uri.Query.Should().Contain("redirect_uri=oob");
        uri.Query.Should().Contain("scope=basic%2Cnetdisk");
        uri.Query.Should().Contain("state=state-1");
    }

    [Fact]
    public void BuildAuthorizeUri_RequiresState()
    {
        var client = new BaiduOAuthClient(new HttpClient());
        var act = () => client.BuildAuthorizeUri("id", " ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ExchangeCodeAsync_ParsesToken_AndDoesNotLeakSecrets()
    {
        var handler = new RecordingHandler(_ => Json("{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":3600}"));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        client.BuildAuthorizeUri("id", "state-1");
        var token = await client.ExchangeCodeAsync("code", "id", "secret", "state-1");
        token.Provider.Should().Be(CloudProviderKind.Baidu);
        token.AccessToken.Should().Be("a");
        token.RefreshToken.Should().Be("r");
        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/oauth/2.0/token");
        handler.Body.Should().Contain("grant_type=authorization_code");
    }

    [Fact]
    public async Task RefreshAsync_RequiresRotatedRefreshToken()
    {
        var handler = new RecordingHandler(_ => Json("{\"access_token\":\"new-a\",\"expires_in\":3600}"));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        var old = new CloudAuthToken(CloudProviderKind.Baidu, "a", "r", DateTimeOffset.UtcNow);
        Func<Task> act = () => client.RefreshAsync(old, "id", "secret");
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task ErrorResponse_IsSanitized()
    {
        var handler = new RecordingHandler(_ => Json("{\"error\":\"invalid_grant\",\"error_description\":\"bad\"}", HttpStatusCode.BadRequest));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        client.BuildAuthorizeUri("id", "state-1");
        var act = () => client.ExchangeCodeAsync("code", "id", "secret", "state-1");
        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.Message.Should().Contain("invalid_grant").And.NotContain("secret");
    }

    [Fact]
    public async Task MalformedTokenTypes_AreReportedAsInvalidData()
    {
        var handler = new RecordingHandler(_ => Json("{\"access_token\":[],\"refresh_token\":\"r\",\"expires_in\":\"bad\"}"));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        client.BuildAuthorizeUri("id", "state-1");
        Func<Task> act = () => client.ExchangeCodeAsync("code", "id", "secret", "state-1");
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task ExchangeCodeAsync_HonorsCancellation()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var handler = new RecordingHandler(_ => Json("{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":3600}"));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        client.BuildAuthorizeUri("id", "state-1");
        Func<Task> act = () => client.ExchangeCodeAsync("code", "id", "secret", "state-1", canceled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExchangeCodeAsync_RejectsStateMismatch()
    {
        var client = new BaiduOAuthClient(new HttpClient());
        client.BuildAuthorizeUri("id", "state-1");
        Func<Task> act = () => client.ExchangeCodeAsync("code", "id", "secret", "wrong");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RefreshAndPersistAsync_SavesOnlyRotatedToken()
    {
        var handler = new RecordingHandler(_ => Json("{\"access_token\":\"new-a\",\"refresh_token\":\"new-r\",\"expires_in\":3600}"));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        var store = new MemorySessionStore();
        var old = new CloudAuthToken(CloudProviderKind.Baidu, "a", "r", DateTimeOffset.UtcNow);
        var updated = await client.RefreshAndPersistAsync(old, "id", "secret", store);
        updated.RefreshToken.Should().Be("new-r");
        store.Saved.Should().BeSameAs(updated);
    }

    [Fact]
    public async Task RefreshAndPersistAsync_PersistsNewTokenEvenWhenCallerCancelsDuringSave()
    {
        var handler = new RecordingHandler(_ => Json("{\"access_token\":\"new-a\",\"refresh_token\":\"new-r\",\"expires_in\":3600}"));
        var client = new BaiduOAuthClient(new HttpClient(handler));
        var store = new CancellationObservingStore();
        using var cancellation = new CancellationTokenSource();
        store.CancelSource = cancellation;
        var old = new CloudAuthToken(CloudProviderKind.Baidu, "a", "r", DateTimeOffset.UtcNow);
        var updated = await client.RefreshAndPersistAsync(old, "id", "secret", store, cancellation.Token);
        updated.RefreshToken.Should().Be("new-r");
        store.Saved!.RefreshToken.Should().Be("new-r");
        store.ReceivedToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public async Task LogoutCoordinator_DeletesBeforePausing()
    {
        var events = new List<string>();
        var store = new MemorySessionStore(events);
        var coordinator = new CloudSessionLogoutCoordinator(store, (provider, _) =>
        {
            events.Add("pause");
            return Task.CompletedTask;
        });
        await coordinator.LogoutAsync(CloudProviderKind.Baidu);
        events.Should().Equal("delete", "pause");
        store.Saved.Should().BeNull();
    }

    private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private sealed class MemorySessionStore : ICloudSessionStore
    {
        private readonly List<string>? _events;
        public MemorySessionStore(List<string>? events = null) => _events = events;
        public CloudAuthToken? Saved { get; private set; }
        public Task SaveAsync(CloudAuthToken token, CancellationToken cancellationToken = default) { Saved = token; return Task.CompletedTask; }
        public Task<CloudAuthToken?> LoadAsync(CloudProviderKind provider, CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task DeleteAsync(CloudProviderKind provider, CancellationToken cancellationToken = default) { Saved = null; _events?.Add("delete"); return Task.CompletedTask; }
    }

    private sealed class CancellationObservingStore : ICloudSessionStore
    {
        public CloudAuthToken? Saved { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public CancellationTokenSource? CancelSource { get; set; }
        public Task SaveAsync(CloudAuthToken token, CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            CancelSource?.Cancel();
            Saved = token;
            return Task.CompletedTask;
        }
        public Task<CloudAuthToken?> LoadAsync(CloudProviderKind provider, CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task DeleteAsync(CloudProviderKind provider, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
