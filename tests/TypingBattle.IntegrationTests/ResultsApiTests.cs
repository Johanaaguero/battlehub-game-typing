using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Flujos de la API de resultados contra MySQL real, incluidos los dos mínimos exigidos por
/// 04-persistencia-y-api-juegos.md: POST /results → GET /results/{matchId}, e historial con varios resultados.
/// Todas las pruebas comparten la base de la clase, así que cada una usa ids propios.
/// </summary>
[Trait("Category", "Integration")]
public class ResultsApiTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    private const string Base = "/api/games/typing";

    // Un servicio con permiso de escritura: POST /results no es para el navegador de un jugador.
    private readonly HttpClient _client = factory.CreateAuthenticatedClient("tester", TestAuth.ResultsWritePermission);

    [Fact]
    public async Task PostResult_LuegoGet_DevuelveElMismoResultado()
    {
        var (matchId, ana, luis) = (NewId("match"), NewId("ana"), NewId("luis"));
        var body = ResultBody(matchId, "2026-09-02T20:00:00Z", "2026-09-02T20:00:47Z",
            (ana, "Ana", 850, 62.4, 96.1), (luis, "Luis", 620, 44.0, 91.5));

        var post = await _client.PostAsJsonAsync($"{Base}/results", body);

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal($"{Base}/results/{matchId}", post.Headers.Location?.OriginalString);

        var get = await _client.GetAsync($"{Base}/results/{matchId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var result = await ReadJson(get);
        Assert.Equal(matchId, (string?)result["matchId"]);
        Assert.Equal("typing", (string?)result["gameType"]);
        Assert.Equal(ana, (string?)result["winnerUserId"]);
        Assert.Equal("t-07", (string?)result["metadata"]!["textId"]);

        var players = result["players"]!.AsArray();
        Assert.Equal(2, players.Count);
        Assert.Equal(ana, (string?)players[0]!["userId"]);
        Assert.Equal("Ana", (string?)players[0]!["displayName"]);
        Assert.Equal(850, (int?)players[0]!["score"]);
        Assert.Equal(luis, (string?)players[1]!["userId"]);

        // Contrato: fechas en UTC, ISO-8601 con sufijo Z.
        var raw = await get.Content.ReadAsStringAsync();
        Assert.Contains("\"startedAt\":\"2026-09-02T20:00:00Z\"", raw);
        Assert.Contains("\"finishedAt\":\"2026-09-02T20:00:47Z\"", raw);
    }

    [Fact]
    public async Task PostResult_ConOffset_SeGuardaEnUtc()
    {
        var matchId = NewId("match");
        var body = ResultBody(matchId, "2026-09-02T14:00:00-06:00", "2026-09-02T14:01:00-06:00", (NewId("ana"), "Ana", 10, null, null));

        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync($"{Base}/results", body)).StatusCode);

        var raw = await _client.GetStringAsync($"{Base}/results/{matchId}");
        Assert.Contains("\"startedAt\":\"2026-09-02T20:00:00Z\"", raw);
    }

    [Fact]
    public async Task History_ConVariasPartidas_LasDevuelveDeLaMasRecienteALaMasAntigua()
    {
        var (ana, luis) = (NewId("ana"), NewId("luis"));
        var (first, second, third) = (NewId("m1"), NewId("m2"), NewId("m3"));

        await PostOk(ResultBody(first, "2026-09-02T20:00:00Z", "2026-09-02T20:01:00Z",
            (ana, "Ana", 300, 30.0, 90.0), (luis, "Luis", 200, 20.0, 80.0)));
        await PostOk(ResultBody(second, "2026-09-02T22:00:00Z", "2026-09-02T22:01:00Z",
            (luis, "Luis", 700, 70.0, 99.0), (ana, "Ana", 600, 60.0, 100.0)));
        await PostOk(ResultBody(third, "2026-09-02T21:00:00Z", "2026-09-02T21:01:00Z",
            (ana, "Ana", 450, null, null)));

        var history = await _client.GetFromJsonAsync<JsonArray>($"{Base}/players/{ana}/history");

        Assert.NotNull(history);
        Assert.Equal(new[] { second, third, first }, history.Select(item => (string)item!["matchId"]!));

        var latest = history[0]!;
        Assert.Equal(600, (int?)latest["score"]);
        Assert.Equal(60.0, (double?)latest["wpm"]);
        Assert.Equal(2, (int?)latest["position"]);
        Assert.Equal(2, (int?)latest["playersCount"]);
        Assert.False((bool?)latest["won"]);

        var solo = history[1]!;
        Assert.Null(solo["wpm"]);
        Assert.Equal(1, (int?)solo["playersCount"]);
        Assert.True((bool?)solo["won"]);

        var paged = await _client.GetFromJsonAsync<JsonArray>($"{Base}/players/{ana}/history?limit=1&offset=1");
        Assert.Equal(third, (string?)Assert.Single(paged!)!["matchId"]);
    }

    [Fact]
    public async Task Stats_AgregaLasPartidasDelJugador()
    {
        var (ana, luis) = (NewId("ana"), NewId("luis"));

        await PostOk(ResultBody(NewId("m"), "2026-09-02T20:00:00Z", "2026-09-02T20:01:00Z",
            (ana, "Ana", 300, 30.0, 90.0), (luis, "Luis", 200, 20.0, 80.0)));
        await PostOk(ResultBody(NewId("m"), "2026-09-02T22:00:00Z", "2026-09-02T22:01:00Z",
            (luis, "Luis", 700, 70.0, 99.0), (ana, "Ana", 600, 60.0, 100.0)));

        var stats = await _client.GetFromJsonAsync<JsonObject>($"{Base}/players/{ana}/stats");

        Assert.NotNull(stats);
        Assert.Equal(ana, (string?)stats["userId"]);
        Assert.Equal(2, (int?)stats["gamesPlayed"]);
        Assert.Equal(1, (int?)stats["wins"]);
        Assert.Equal(0.5, (double?)stats["winRate"]);
        Assert.Equal(450.0, (double?)stats["averageScore"]);
        Assert.Equal(600, (int?)stats["bestScore"]);
        Assert.Equal(45.0, (double?)stats["averageWpm"]);
        Assert.Equal(60.0, (double?)stats["bestWpm"]);
        Assert.Equal(95.0, (double?)stats["averageAccuracy"]);
        Assert.Equal("2026-09-02T22:01:00Z", (string?)stats["lastPlayedAt"]);
    }

    [Fact]
    public async Task Stats_JugadorSinPartidas_DevuelveCeros()
    {
        var userId = NewId("nadie");

        var stats = await _client.GetFromJsonAsync<JsonObject>($"{Base}/players/{userId}/stats");

        Assert.Equal(0, (int?)stats!["gamesPlayed"]);
        Assert.Null(stats["averageWpm"]);
        Assert.Null(stats["lastPlayedAt"]);
        Assert.Empty((await _client.GetFromJsonAsync<JsonArray>($"{Base}/players/{userId}/history"))!);
    }

    [Fact]
    public async Task UserId_DistingueMayusculasYCaracteresDeAuth0()
    {
        var id = NewId("auth0|Ana");
        await PostOk(ResultBody(NewId("m"), "2026-09-02T20:00:00Z", "2026-09-02T20:01:00Z", (id, "Ana", 100, null, null)));

        var exact = await _client.GetFromJsonAsync<JsonObject>($"{Base}/players/{Uri.EscapeDataString(id)}/stats");
        var lower = await _client.GetFromJsonAsync<JsonObject>($"{Base}/players/{Uri.EscapeDataString(id.ToLowerInvariant())}/stats");

        Assert.Equal(1, (int?)exact!["gamesPlayed"]);
        Assert.Equal(0, (int?)lower!["gamesPlayed"]);
    }

    [Fact]
    public async Task PostResult_Duplicado_Devuelve409()
    {
        var body = ResultBody(NewId("match"), "2026-09-02T20:00:00Z", "2026-09-02T20:01:00Z", (NewId("ana"), "Ana", 1, null, null));
        await PostOk(body);

        var again = await _client.PostAsJsonAsync($"{Base}/results", body);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task PostResult_Concurrente_SoloGuardaUno()
    {
        var body = ResultBody(NewId("match"), "2026-09-02T20:00:00Z", "2026-09-02T20:01:00Z", (NewId("ana"), "Ana", 1, null, null));

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.PostAsJsonAsync($"{Base}/results", body)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
    }

    [Fact]
    public async Task PostResult_Invalido_Devuelve400ConErroresPorCampo()
    {
        var body = new
        {
            matchId = "",
            gameType = "trivia",
            players = Array.Empty<object>(),
            startedAt = "2026-09-02T20:00:00",
            finishedAt = "2026-09-02T20:01:00Z",
        };

        var response = await _client.PostAsJsonAsync($"{Base}/results", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var errors = (await ReadJson(response))["errors"]!.AsObject();
        Assert.Contains("matchId", errors.Select(e => e.Key));
        Assert.Contains("gameType", errors.Select(e => e.Key));
        Assert.Contains("players", errors.Select(e => e.Key));
        Assert.Contains("startedAt", errors.Select(e => e.Key));
    }

    [Fact]
    public async Task GetResult_Inexistente_Devuelve404()
    {
        var response = await _client.GetAsync($"{Base}/results/{NewId("no-existe")}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_ConMySqlDisponible_Devuelve200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task PostOk(object body)
    {
        var response = await _client.PostAsJsonAsync($"{Base}/results", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<JsonObject> ReadJson(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    /// <summary>Arma el cuerpo de POST /results. El orden de los jugadores es el ranking; el primero gana.</summary>
    private static object ResultBody(
        string matchId,
        string startedAt,
        string finishedAt,
        params (string UserId, string Name, int Score, double? Wpm, double? Accuracy)[] players) => new
    {
        matchId,
        gameType = "typing",
        players = players.Select(p => new { userId = p.UserId, displayName = p.Name, score = p.Score }),
        startedAt = DateTimeOffset.Parse(startedAt),
        finishedAt = DateTimeOffset.Parse(finishedAt),
        winnerUserId = players[0].UserId,
        metadata = new
        {
            textId = "t-07",
            players = players
                .Where(p => p.Wpm.HasValue || p.Accuracy.HasValue)
                .Select(p => new { userId = p.UserId, wpm = p.Wpm, accuracy = p.Accuracy }),
        },
    };
}
