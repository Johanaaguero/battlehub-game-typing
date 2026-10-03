using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TypingBattle.Api.Matchmaking;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Matchmaking;

[Trait("Category", "Unit")]
public class MatchmakingClientTests
{
    private const string TokenUrl = "https://tenant.test/oauth/token";

    private sealed record SentRequest(HttpMethod Method, Uri Uri, string? Authorization, string Body);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        public IEnumerable<SentRequest> FinishRequests => Requests.Where(r => r.Uri.AbsoluteUri != TokenUrl);

        public IEnumerable<SentRequest> TokenRequests => Requests.Where(r => r.Uri.AbsoluteUri == TokenUrl);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
            return respond(request);
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static MatchmakingOptions Settings(bool withAuth0 = true) => new()
    {
        BaseUrl = "http://matchmaking.test",
        RetryDelayMilliseconds = 0,
        Auth0 = withAuth0
            ? new Auth0MachineOptions
            {
                Domain = "tenant.test",
                ClientId = "typing-m2m",
                ClientSecret = "secreto-de-prueba",
                Audience = "https://api.battlehub.local/matchmaking",
            }
            : new Auth0MachineOptions(),
    };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static (MatchmakingClient Client, FakeHandler Handler) Create(
        MatchmakingOptions settings,
        Func<HttpRequestMessage, HttpResponseMessage> finishResponse,
        TimeProvider? clock = null)
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsoluteUri == TokenUrl
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"token-m2m","expires_in":3600,"token_type":"Bearer"}""", Encoding.UTF8, "application/json"),
            }
            : finishResponse(request));
        var factory = new FakeHttpClientFactory(handler);
        var options = Options.Create(settings);
        var tokens = new Auth0MachineTokenProvider(factory, options, clock ?? TimeProvider.System);
        return (new MatchmakingClient(factory, tokens, options, NullLogger<MatchmakingClient>.Instance), handler);
    }

    [Fact]
    public async Task SinBaseUrl_NoLlamaANadie()
    {
        var settings = Settings();
        settings.BaseUrl = "";
        var (client, handler) = Create(settings, _ => Status(HttpStatusCode.NoContent));

        var ok = await client.NotifyFinishedAsync("match-001");

        Assert.False(ok);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Avisa_ConPostSinCuerpo_YTokenM2M()
    {
        var (client, handler) = Create(Settings(), _ => Status(HttpStatusCode.NoContent));

        var ok = await client.NotifyFinishedAsync("match 001");

        Assert.True(ok);
        var tokenRequest = Assert.Single(handler.TokenRequests);
        using var credentials = JsonDocument.Parse(tokenRequest.Body);
        Assert.Equal("client_credentials", credentials.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal("typing-m2m", credentials.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("https://api.battlehub.local/matchmaking", credentials.RootElement.GetProperty("audience").GetString());

        var finish = Assert.Single(handler.FinishRequests);
        Assert.Equal(HttpMethod.Post, finish.Method);
        Assert.Equal("http://matchmaking.test/api/matches/match%20001/finish", finish.Uri.AbsoluteUri);
        Assert.Equal("Bearer token-m2m", finish.Authorization);
        Assert.Equal("", finish.Body);
    }

    [Fact]
    public async Task ReutilizaElToken_HastaQueVence()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 3, 20, 0, 0, TimeSpan.Zero));
        var (client, handler) = Create(Settings(), _ => Status(HttpStatusCode.NoContent), clock);

        await client.NotifyFinishedAsync("m-1");
        await client.NotifyFinishedAsync("m-2");
        Assert.Single(handler.TokenRequests);

        clock.Now = clock.Now.AddHours(1);
        await client.NotifyFinishedAsync("m-3");
        Assert.Equal(2, handler.TokenRequests.Count());
    }

    [Fact]
    public async Task SinCredencialesAuth0_AvisaSinToken()
    {
        var (client, handler) = Create(Settings(withAuth0: false), _ => Status(HttpStatusCode.OK));

        Assert.True(await client.NotifyFinishedAsync("m-1"));

        Assert.Empty(handler.TokenRequests);
        Assert.Null(Assert.Single(handler.FinishRequests).Authorization);
    }

    [Fact]
    public async Task ReintentaErroresTemporales()
    {
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.ServiceUnavailable, HttpStatusCode.NoContent]);
        var (client, handler) = Create(Settings(), _ => Status(responses.Dequeue()));

        Assert.True(await client.NotifyFinishedAsync("m-1"));

        Assert.Equal(2, handler.FinishRequests.Count());
    }

    [Fact]
    public async Task ReintentaFallosDeRed()
    {
        var calls = 0;
        var (client, handler) = Create(Settings(), _ => ++calls == 1
            ? throw new HttpRequestException("Conexión rechazada")
            : Status(HttpStatusCode.NoContent));

        Assert.True(await client.NotifyFinishedAsync("m-1"));

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task NoReintenta_CuandoMatchmakingRechaza(HttpStatusCode status)
    {
        var (client, handler) = Create(Settings(), _ => Status(status));

        Assert.False(await client.NotifyFinishedAsync("m-1"));

        Assert.Single(handler.FinishRequests);
    }

    [Fact]
    public async Task SeRinde_DespuesDeMaxAttempts()
    {
        var (client, handler) = Create(Settings(), _ => Status(HttpStatusCode.InternalServerError));

        Assert.False(await client.NotifyFinishedAsync("m-1"));

        Assert.Equal(3, handler.FinishRequests.Count());
    }

    [Theory]
    [InlineData("tenant.us.auth0.com", "https://tenant.us.auth0.com/oauth/token")]
    [InlineData("https://tenant.us.auth0.com/", "https://tenant.us.auth0.com/oauth/token")]
    public void TokenEndpoint_AceptaElDominioConOSinEsquema(string domain, string expected)
    {
        Assert.Equal(expected, Auth0MachineTokenProvider.BuildTokenEndpoint(domain).AbsoluteUri);
    }

    [Fact]
    public async Task Cola_SoloEncolaLasPartidasRecienGuardadas()
    {
        var queue = new MatchFinishedQueue();
        var saved = new GameResultDto(
            "match-001", "typing", [new PlayerResultDto("ana", "Ana", 600)],
            DateTime.UtcNow, DateTime.UtcNow, "ana", JsonDocument.Parse("{}").RootElement);

        await queue.NotifyMatchFinishedAsync(" match-001 ", new SaveResultOutcome.Created(saved));
        await queue.NotifyMatchFinishedAsync("match-001", new SaveResultOutcome.AlreadyExists("match-001"));
        await queue.NotifyMatchFinishedAsync("match-002", new SaveResultOutcome.Invalid(new Dictionary<string, string[]>()));

        Assert.True(queue.Reader.TryRead(out var matchId));
        Assert.Equal("match-001", matchId);
        Assert.False(queue.Reader.TryRead(out _));
    }
}
