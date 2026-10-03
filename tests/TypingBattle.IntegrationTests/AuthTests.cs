using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TypingBattle.Api.Hubs;

namespace TypingBattle.IntegrationTests;

/// <summary>Autenticación y autorización con Auth:Mode=Development (identidad por encabezado o URL).</summary>
[Trait("Category", "Integration")]
public class DevelopmentAuthTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    [Fact]
    public async Task Health_NoExigeUsuario()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_SinUsuario_Devuelve401()
    {
        var response = await factory.CreateClient().GetAsync("/api/games/typing/players/ana/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostResults_SinPermisoDeEscritura_Devuelve403()
    {
        var player = factory.CreateAuthenticatedClient("ana");

        var response = await player.PostAsync("/api/games/typing/results", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Hub_SinUsuario_RechazaLaConexion()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/typing"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task Hub_NoDejaActuarEnNombreDeOtroJugador()
    {
        var matchId = $"match-{Guid.NewGuid():N}";
        await using var ana = factory.CreateHubConnection("ana");
        var failures = new ConcurrentQueue<string>();
        ana.On<JoinFailed>("joinFailed", f => failures.Enqueue(f.Reason));
        await ana.StartAsync();

        Assert.False(await ana.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, "luis", "Luis")));
        Assert.False(await ana.InvokeAsync<bool>("SendPlayerUpdate", new PlayerUpdateDto(matchId, "ana", null, 50, 100, DateTime.UtcNow)));

        await WaitUntilAsync(() => failures.Contains("user_mismatch"));
    }

    [Fact]
    public async Task Hub_SoloUnJugadorDeLaPartidaPuedeTerminarla()
    {
        var matchId = $"match-{Guid.NewGuid():N}";
        await using var ana = factory.CreateHubConnection("ana");
        await using var intruso = factory.CreateHubConnection("intruso");
        var outcomes = new ConcurrentQueue<string>();
        intruso.On<MatchEndedNotification>("matchEnded", n => outcomes.Enqueue(n.Outcome));
        await ana.StartAsync();
        await intruso.StartAsync();
        Assert.True(await ana.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, "ana", "Ana")));

        await intruso.InvokeAsync("EndMatch", new EndMatchRequest(matchId, "intruso", null, null, null, null, null));

        await WaitUntilAsync(() => outcomes.Contains("invalid"));
        var result = await factory.CreateAuthenticatedClient("ana").GetAsync($"/api/games/typing/results/{matchId}");
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    private sealed record JoinFailed(string MatchId, string Reason);

    internal static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}

/// <summary>
/// Auth:Mode=Auth0 con tokens firmados de verdad. En lugar del tenant (que exigiría red) se usa una clave de prueba:
/// la validación de firma, emisor, audiencia y vigencia es la misma que con Auth0.
/// </summary>
[Trait("Category", "Integration")]
public class Auth0JwtTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    private const string Issuer = "https://typing-tests.auth0.local/";
    private const string Audience = "https://api.battlehub.local/typing";

    private static readonly SymmetricSecurityKey Key =
        new(Encoding.UTF8.GetBytes("clave-solo-para-las-pruebas-de-typing-battle-0123456789"));

    private WebApplicationFactory<Program> Auth0App(string requiredPermission = "") =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Mode", "Auth0");
            builder.UseSetting("Auth:Domain", "typing-tests.auth0.local");
            builder.UseSetting("Auth:Audience", Audience);
            builder.UseSetting("Auth:RequiredPermission", requiredPermission);
            builder.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    configuration.SigningKeys.Add(Key);
                    jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                }));
        });

    private static string Token(string sub, string audience = Audience, params string[] permissions)
    {
        var claims = new Dictionary<string, object> { ["sub"] = sub };
        if (permissions.Length > 0)
        {
            claims["permissions"] = permissions;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        });
    }

    private static async Task<HttpStatusCode> GetStatsAsync(WebApplicationFactory<Program> app, string? token, string query = "")
    {
        var client = app.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return (await client.GetAsync($"/api/games/typing/players/auth0%7Cana/stats{query}")).StatusCode;
    }

    [Fact]
    public async Task TokenValido_Accede()
    {
        await using var app = Auth0App();

        Assert.Equal(HttpStatusCode.OK, await GetStatsAsync(app, Token("auth0|ana")));
    }

    [Fact]
    public async Task SinTokenOConAudienciaAjena_Devuelve401()
    {
        await using var app = Auth0App();

        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatsAsync(app, null));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatsAsync(app, Token("auth0|ana", audience: "https://api.battlehub.local/profile")));
    }

    [Fact]
    public async Task ElTokenEnLaUrl_SoloSirveParaElHub()
    {
        await using var app = Auth0App();

        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatsAsync(app, null, $"?access_token={Token("auth0|ana")}"));
    }

    [Fact]
    public async Task ElIdentificadorDelJugador_EsElSubDelToken()
    {
        await using var app = Auth0App();
        var matchId = $"match-{Guid.NewGuid():N}";

        // Como un WebSocket del navegador: el token viaja en la URL.
        var url = new Uri(app.Server.BaseAddress, $"/hubs/typing?access_token={Token("auth0|ana")}");
        await using var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();

        Assert.False(await connection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, "auth0|luis", "Luis")));
        Assert.True(await connection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, "auth0|ana", "Ana")));
    }

    [Fact]
    public async Task ConPermisoRequerido_SinElPermiso_Devuelve403()
    {
        await using var app = Auth0App(requiredPermission: "games.typing.play");

        Assert.Equal(HttpStatusCode.Forbidden, await GetStatsAsync(app, Token("auth0|ana")));
        Assert.Equal(HttpStatusCode.OK, await GetStatsAsync(app, Token("auth0|ana", Audience, "games.typing.play")));
    }
}
