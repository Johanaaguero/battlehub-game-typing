using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TypingBattle.Api.Hubs;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Flujo real de una partida por el hub, sin preparar nada a mano: dos jugadores entran a una partida que
/// el servicio no conocía, envían métricas sin puntaje y ambos piden terminarla cuando se les acaba el tiempo.
/// </summary>
[Trait("Category", "Integration")]
public class TypingHubFlowTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Partida_SinPrepararAntes_SeJuegaYSeGuardaUnaSolaVez()
    {
        var matchId = $"match-{Guid.NewGuid():N}";
        var (ana, luis) = ($"ana-{Guid.NewGuid():N}", $"luis-{Guid.NewGuid():N}");

        await using var anaConnection = await ConnectAsync();
        await using var luisConnection = await ConnectAsync();
        var anaEvents = Listen(anaConnection);
        var luisEvents = Listen(luisConnection);

        Assert.True(await anaConnection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, ana, "Ana")));
        Assert.True(await luisConnection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, luis, "Luis")));

        // El cliente no envía puntaje: el servidor lo calcula (velocidad × precisión × 10) y lo retransmite.
        Assert.True(await anaConnection.InvokeAsync<bool>("SendPlayerUpdate", new PlayerUpdateDto(matchId, ana, null, 40.0, 90.0, DateTime.UtcNow)));
        Assert.True(await luisConnection.InvokeAsync<bool>("SendPlayerUpdate", new PlayerUpdateDto(matchId, luis, null, 60.0, 100.0, DateTime.UtcNow)));

        var luisUpdate = await WaitForAsync(anaEvents.Updates, u => u.UserId == luis);
        Assert.Equal(600, luisUpdate.Score);

        // A los dos se les acaba el tiempo a la vez: solo uno guarda el resultado.
        await Task.WhenAll(
            anaConnection.InvokeAsync("EndMatch", EndRequest(matchId, ana)),
            luisConnection.InvokeAsync("EndMatch", EndRequest(matchId, luis)));

        await WaitForAsync(anaEvents.Ended, e => e.Outcome == "created");
        await WaitForAsync(luisEvents.Ended, e => e.Outcome == "created");
        await WaitUntilAsync(() => anaEvents.Ended.Count + luisEvents.Ended.Count >= 3);
        await Task.Delay(200);

        // Todos reciben un único "created"; "already_exists" solo le llega a quien pidió terminar de segundo.
        Assert.Single(anaEvents.Ended, e => e.Outcome == "created");
        Assert.Single(luisEvents.Ended, e => e.Outcome == "created");
        Assert.Single(anaEvents.Ended.Concat(luisEvents.Ended), e => e.Outcome == "already_exists");

        // Una partida terminada no admite jugadores nuevos.
        Assert.False(await anaConnection.InvokeAsync<bool>("JoinMatch", new JoinMatchRequest(matchId, ana, "Ana")));

        var response = await _client.GetAsync($"/api/games/typing/results/{matchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        var players = result["players"]!.AsArray();
        Assert.Equal(luis, (string?)players[0]!["userId"]);
        Assert.Equal(600, (int?)players[0]!["score"]);
        Assert.Equal(ana, (string?)players[1]!["userId"]);
        Assert.Equal(360, (int?)players[1]!["score"]);
        Assert.Equal(luis, (string?)result["winnerUserId"]);

        // El inicio es la entrada del primer jugador y el fin, el pedido de terminar: ambos los pone el servidor.
        var startedAt = DateTimeOffset.Parse((string)result["startedAt"]!);
        var finishedAt = DateTimeOffset.Parse((string)result["finishedAt"]!);
        Assert.True(finishedAt >= startedAt);
    }

    private static EndMatchRequest EndRequest(string matchId, string userId) =>
        new(matchId, userId, null, null, null, null, null);

    private async Task<HubConnection> ConnectAsync()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/typing"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    private sealed class HubEvents
    {
        public ConcurrentQueue<PlayerUpdateNotification> Updates { get; } = new();

        public ConcurrentQueue<MatchEndedNotification> Ended { get; } = new();
    }

    private static HubEvents Listen(HubConnection connection)
    {
        var events = new HubEvents();
        connection.On<PlayerUpdateNotification>("playerUpdate", events.Updates.Enqueue);
        connection.On<MatchEndedNotification>("matchEnded", events.Ended.Enqueue);
        return events;
    }

    private static async Task<T> WaitForAsync<T>(ConcurrentQueue<T> queue, Func<T, bool> match)
        where T : class
    {
        T? found = null;
        await WaitUntilAsync(() => (found = queue.FirstOrDefault(match)) is not null);
        return found!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
