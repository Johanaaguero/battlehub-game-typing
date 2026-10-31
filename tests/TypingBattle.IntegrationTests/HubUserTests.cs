using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using TypingBattle.Api.Hubs;

namespace TypingBattle.IntegrationTests;

/// <summary>El juego no se autentica (R-30): la API y el hub no piden credenciales y el jugador viaja en cada petición.</summary>
[Trait("Category", "Integration")]
public class HubUserTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    [Fact]
    public async Task Health_ResponderSinUsuario()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_NoExigeAutenticacion()
    {
        var response = await factory.CreateClient().GetAsync("/api/games/typing/players/ana/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Hub_AceptaLaConexionSinCredenciales()
    {
        await using var connection = factory.CreateHubConnection();

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Hub_RechazaUnCurrentUserVacio()
    {
        var matchId = $"match-{Guid.NewGuid():N}";
        await using var connection = factory.CreateHubConnection();
        var failures = new ConcurrentQueue<string>();
        connection.On<HubFailure>("joinFailed", f => failures.Enqueue(f.Reason));
        connection.On<HubFailure>("updateFailed", f => failures.Enqueue(f.Reason));
        await connection.StartAsync();

        Assert.False(await connection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, " ", "Ana")));
        Assert.False(await connection.InvokeAsync<bool>("SendPlayerUpdate", new PlayerUpdateDto(matchId, "", null, 50, 100, DateTime.UtcNow)));

        await WaitUntilAsync(() => failures.Count(reason => reason == "user_required") == 2);
    }

    [Fact]
    public async Task Hub_SoloUnJugadorDeLaPartidaPuedeTerminarla()
    {
        var matchId = $"match-{Guid.NewGuid():N}";
        await using var ana = factory.CreateHubConnection();
        await using var intruso = factory.CreateHubConnection();
        var outcomes = new ConcurrentQueue<string>();
        intruso.On<MatchEndedNotification>("matchEnded", n => outcomes.Enqueue(n.Outcome));
        await ana.StartAsync();
        await intruso.StartAsync();
        Assert.True(await ana.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, "ana", "Ana")));

        await intruso.InvokeAsync("EndMatch", new EndMatchRequest(matchId, "intruso", null, null, null, null, null));

        await WaitUntilAsync(() => outcomes.Contains("invalid"));
        var result = await factory.CreateClient().GetAsync($"/api/games/typing/results/{matchId}");
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    private sealed record HubFailure(string MatchId, string Reason);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
