using System.Text.Json;
using Moq;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Hubs;

/// <summary>
/// Las dependencias de <see cref="TypingMatchService"/> que salen del proceso, el servicio de resultados (MySQL) y el reloj,
/// se simulan con Moq: estas pruebas no tocan base de datos, red ni la hora real.
/// </summary>
[Trait("Category", "Unit")]
public class TypingMatchServiceTests
{
    private static readonly string[] InvalidPlayerErrors = ["error"];
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Las solicitudes que el servicio de resultados simulado recibió, en orden.</summary>
    private sealed class SavedRequests
    {
        public List<SaveResultRequest> All { get; } = [];

        public SaveResultRequest Last => All[^1];
    }

    private static Mock<TimeProvider> CreateClock(DateTimeOffset now)
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(now);
        return clock;
    }

    /// <summary>
    /// Servicio de resultados simulado. Devuelve, en orden, los resultados indicados; cuando se acaban, acepta la
    /// solicitud y la devuelve como creada. Todo lo que recibe queda en <paramref name="saved"/>.
    /// </summary>
    private static Mock<IResultsService> CreateResultsService(SavedRequests saved, params SaveResultOutcome[] outcomes)
    {
        var queue = new Queue<SaveResultOutcome>(outcomes);
        var results = new Mock<IResultsService>();
        results
            .Setup(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SaveResultRequest, CancellationToken>((request, _) => saved.All.Add(request))
            .ReturnsAsync((SaveResultRequest request, CancellationToken _) =>
                queue.Count > 0 ? queue.Dequeue() : new SaveResultOutcome.Created(ToDto(request)));
        return results;
    }

    private static GameResultDto ToDto(SaveResultRequest request) => new(
        request.MatchId!,
        request.GameType!,
        [.. request.Players!.Select(p => new PlayerResultDto(p!.UserId!, p.DisplayName ?? string.Empty, p.Score ?? 0))],
        request.StartedAt!.Value,
        request.FinishedAt!.Value,
        request.WinnerUserId,
        request.Metadata ?? JsonDocument.Parse("{}").RootElement);

    private static (TypingMatchService Service, TypingMatchStore Store, SavedRequests Saved, Mock<IResultsService> Results)
        CreateService(DateTimeOffset? now = null, Mock<TimeProvider>? clock = null, params SaveResultOutcome[] outcomes)
    {
        var store = new TypingMatchStore();
        var saved = new SavedRequests();
        var results = CreateResultsService(saved, outcomes);
        var service = new TypingMatchService(store, results.Object, (clock ?? CreateClock(now ?? Start)).Object);
        return (service, store, saved, results);
    }

    private static EndMatchRequest EndRequest(string matchId, string user = "u1") =>
        new(matchId, user, null, null, null, null, null);

    [Fact]
    public void Constructor_Rejects_MissingDependencies()
    {
        var results = new Mock<IResultsService>().Object;
        var clock = CreateClock(Start).Object;
        var store = new TypingMatchStore();

        Assert.Throws<ArgumentNullException>(() => new TypingMatchService(null!, results, clock));
        Assert.Throws<ArgumentNullException>(() => new TypingMatchService(store, null!, clock));
        Assert.Throws<ArgumentNullException>(() => new TypingMatchService(store, results, null!));
    }

    [Fact]
    public void JoinMatch_Succeeds_WhenMatchExists()
    {
        var (svc, store, _, _) = CreateService();
        store.GetOrCreate("m-1");

        var ok = svc.TryJoin("m-1", "u1", "Ana");

        Assert.True(ok);
        Assert.True(store.TryGet("m-1", out var state));
        Assert.True(state.TryGetPlayer("u1", out var player));
        Assert.Equal("Ana", player.DisplayName);
    }

    [Fact]
    public void JoinMatch_CreatesMatch_WhenItDoesNotExistYet()
    {
        // Matchmaking crea la sala; el servicio de Typing la conoce recién con el primer jugador que entra.
        var (svc, store, _, _) = CreateService();

        var ok = svc.TryJoin("m-new", "u1", "Ana");

        Assert.True(ok);
        Assert.True(store.TryGet("m-new", out var state));
        Assert.True(state.TryGetPlayer("u1", out var player));
        Assert.Equal("Ana", player.DisplayName);
    }

    [Fact]
    public void JoinMatch_RecordsStartedAt_OnFirstJoinOnly()
    {
        var clock = CreateClock(Start);
        var (svc, store, _, _) = CreateService(clock: clock);

        svc.TryJoin("m-start", "u1", "Ana");
        clock.Setup(c => c.GetUtcNow()).Returns(Start.AddSeconds(5));
        svc.TryJoin("m-start", "u2", "Luis");

        Assert.True(store.TryGet("m-start", out var state));
        Assert.Equal(Start.UtcDateTime, state.StartedAt);
        clock.Verify(c => c.GetUtcNow(), Times.Once);
    }

    [Fact]
    public void JoinMatch_Fails_WhenMatchAlreadyEnded()
    {
        var (svc, store, _, _) = CreateService();
        store.GetOrCreate("m-ended").MarkFinished(Start.UtcDateTime);

        var ok = svc.TryJoin("m-ended", "u1", "Ana");

        Assert.False(ok);
    }

    [Theory]
    [InlineData("", "u1")]
    [InlineData("m-1", " ")]
    public void JoinMatch_Fails_WhenIdsAreMissing(string matchId, string userId)
    {
        var (svc, store, _, _) = CreateService();

        Assert.False(svc.TryJoin(matchId, userId, "Ana"));
        Assert.Empty(store.ListAll());
    }

    [Fact]
    public void IsPlayer_IsTrueOnlyWhileThePlayerIsInTheMatch()
    {
        var (svc, _, _, _) = CreateService();

        Assert.False(svc.IsPlayer("m-1", "u1"));
        svc.TryJoin("m-1", "u1", "Ana");
        Assert.True(svc.IsPlayer("m-1", "u1"));
        svc.TryLeave("m-1", "u1");
        Assert.False(svc.IsPlayer("m-1", "u1"));
    }

    [Theory]
    [InlineData("", "u1")]
    [InlineData("m-1", " ")]
    public void IsPlayer_IsFalse_WhenIdsAreMissing(string matchId, string userId)
    {
        var (svc, _, _, _) = CreateService();
        svc.TryJoin("m-1", "u1", "Ana");

        Assert.False(svc.IsPlayer(matchId, userId));
    }

    [Fact]
    public void LeaveMatch_RemovesPlayer()
    {
        var (svc, store, _, _) = CreateService();

        svc.TryJoin("m-2", "u1", "Ana");
        var left = svc.TryLeave("m-2", "u1");

        Assert.True(left);
        Assert.True(store.TryGet("m-2", out var state));
        Assert.False(state.TryGetPlayer("u1", out _));
    }

    [Fact]
    public void LeaveMatch_Fails_WhenMatchDoesNotExist()
    {
        var (svc, _, _, _) = CreateService();

        Assert.False(svc.TryLeave("m-x", "u1"));
    }

    [Fact]
    public void UpdateMetrics_CalculatesScore_WhenClientDoesNotSendIt()
    {
        var (svc, store, _, _) = CreateService();
        store.GetOrCreate("m-score");

        Assert.True(svc.TryUpdatePlayerMetrics("m-score", "u1", null, 60.0, 100.0, out var first));
        // Solo cambia la precisión: el puntaje se recalcula con la velocidad ya guardada.
        Assert.True(svc.TryUpdatePlayerMetrics("m-score", "u1", null, null, 50.0, out var second));

        Assert.Equal(600, first);
        Assert.Equal(300, second);
        Assert.True(store.TryGet("m-score", out var state));
        Assert.True(state.TryGetPlayer("u1", out var player));
        Assert.Equal(300, player.Score);
    }

    [Fact]
    public void UpdateMetrics_CreatesPlayer_WhenAbsent()
    {
        var (svc, store, _, _) = CreateService();
        store.GetOrCreate("m-3");

        var ok = svc.TryUpdatePlayerMetrics("m-3", "u42", 123, 45.6, 98.7);

        Assert.True(ok);
        Assert.True(store.TryGet("m-3", out var state));
        Assert.True(state.TryGetPlayer("u42", out var p));
        Assert.Equal(123, p.Score);
        Assert.Equal(45.6, p.Wpm);
        Assert.Equal(98.7, p.Accuracy);
    }

    [Fact]
    public void UpdateMetrics_Fails_WhenMatchNotExist()
    {
        var (svc, _, _, _) = CreateService();

        var ok = svc.TryUpdatePlayerMetrics("m-x", "u1", 1, null, null);

        Assert.False(ok);
    }

    [Fact]
    public void WpmAndAccuracy_Are_NotCalculated_ServerSide()
    {
        // The service stores and forwards WPM/accuracy provided by clients; it does not compute them.
        var (svc, store, _, _) = CreateService();
        store.GetOrCreate("m-7");

        svc.TryUpdatePlayerMetrics("m-7", "u1", 42, 55.5, 99.9);

        var p = store.GetOrCreate("m-7").GetOrCreatePlayer("u1");
        Assert.Equal(55.5, p.Wpm);
        Assert.Equal(99.9, p.Accuracy);
    }

    [Fact]
    public async Task EndMatch_UsesJoinTime_AsStartedAt()
    {
        var clock = CreateClock(Start);
        var (svc, _, saved, _) = CreateService(clock: clock);

        svc.TryJoin("m-times", "u1", "Ana");
        clock.Setup(c => c.GetUtcNow()).Returns(Start.AddSeconds(60));
        await svc.EndMatchAsync(EndRequest("m-times"));

        Assert.Equal(Start.UtcDateTime, saved.Last.StartedAt);
        Assert.Equal(Start.AddSeconds(60).UtcDateTime, saved.Last.FinishedAt);
    }

    [Fact]
    public async Task EndMatch_UsesTheDatesOfTheRequest_WhenTheyAreSent()
    {
        var (svc, _, saved, _) = CreateService();
        svc.TryJoin("m-dates", "u1", "Ana");
        var request = new EndMatchRequest(
            "m-dates", "u1", null, Start.UtcDateTime.AddMinutes(-2), Start.UtcDateTime.AddMinutes(-1), null, null);

        await svc.EndMatchAsync(request);

        Assert.Equal(request.StartedAt, saved.Last.StartedAt);
        Assert.Equal(request.FinishedAt, saved.Last.FinishedAt);
    }

    [Fact]
    public async Task EndMatch_UsesUserId_WhenPlayerHasNoDisplayName()
    {
        var (svc, store, saved, _) = CreateService();
        store.GetOrCreate("m-noname").GetOrCreatePlayer("u1").UpdateMetrics(10, null, null);

        await svc.EndMatchAsync(EndRequest("m-noname"));

        var player = Assert.Single(saved.Last.Players!);
        Assert.Equal("u1", player!.DisplayName);
    }

    [Fact]
    public async Task EndMatch_RanksPlayersByScore_AndTheWinnerIsTheHighestScore()
    {
        var (svc, store, saved, _) = CreateService();
        var state = store.GetOrCreate("m-rank");
        state.GetOrCreatePlayer("u1", "Ana").UpdateMetrics(300, 30.0, 90.0);
        state.GetOrCreatePlayer("u2", "Luis").UpdateMetrics(600, 60.0, 100.0);

        await svc.EndMatchAsync(EndRequest("m-rank"));

        Assert.Equal(["u2", "u1"], saved.Last.Players!.Select(p => p!.UserId));
        Assert.Equal("u2", saved.Last.WinnerUserId);
    }

    [Fact]
    public async Task EndMatch_RespectsTheWinnerOfTheRequest_WhenItIsSent()
    {
        var (svc, store, saved, _) = CreateService();
        var state = store.GetOrCreate("m-winner");
        state.GetOrCreatePlayer("u1", "Ana").UpdateMetrics(300, null, null);
        state.GetOrCreatePlayer("u2", "Luis").UpdateMetrics(600, null, null);

        await svc.EndMatchAsync(EndRequest("m-winner") with { WinnerUserId = "u1" });

        Assert.Equal("u1", saved.Last.WinnerUserId);
    }

    [Fact]
    public async Task EndMatch_Builds_SaveRequest_And_Returns_Created()
    {
        var (svc, store, saved, results) = CreateService();
        var state = store.GetOrCreate("m-4");
        state.GetOrCreatePlayer("u1", "Ana").UpdateMetrics(300, 30.0, 90.0);
        state.GetOrCreatePlayer("u2", "Luis").UpdateMetrics(600, 60.0, 100.0);

        var outcome = await svc.EndMatchAsync(EndRequest("m-4"));

        Assert.IsType<SaveResultOutcome.Created>(outcome);
        results.Verify(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("m-4", saved.Last.MatchId);
        Assert.Equal(ResultValidator.GameType, saved.Last.GameType);
        Assert.Equal(2, saved.Last.Players!.Count);
        // metadata contains players with wpm/accuracy
        var meta = saved.Last.Metadata;
        Assert.True(meta.HasValue);
        Assert.Equal(JsonValueKind.Object, meta.Value.ValueKind);
        Assert.True(meta.Value.TryGetProperty("players", out var playersArr));
        Assert.Equal(2, playersArr.GetArrayLength());
    }

    [Fact]
    public async Task EndMatch_PassesTheCancellationToken_ToTheResultsService()
    {
        var (svc, store, _, results) = CreateService();
        store.GetOrCreate("m-token");
        using var cts = new CancellationTokenSource();

        await svc.EndMatchAsync(EndRequest("m-token"), cts.Token);

        results.Verify(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task EndMatch_DoesNotCallTheResultsService_WhenTheMatchDoesNotExist()
    {
        var (svc, _, _, results) = CreateService();

        var outcome = await svc.EndMatchAsync(EndRequest("m-missing"));

        var invalid = Assert.IsType<SaveResultOutcome.Invalid>(outcome);
        Assert.Equal(["Partida no encontrada."], invalid.Errors["matchId"]);
        results.Verify(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EndMatch_Rejects_ANullRequest()
    {
        var (svc, _, _, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.EndMatchAsync(null!));
    }

    [Fact]
    public async Task EndMatch_Returns_AlreadyExists_On_Second_Call()
    {
        var created = new GameResultDto(
            "m-5",
            ResultValidator.GameType,
            [new PlayerResultDto("u1", "u1", 10)],
            Start.UtcDateTime,
            Start.UtcDateTime,
            "u1",
            JsonDocument.Parse("{}").RootElement);
        var (svc, store, _, results) = CreateService(
            outcomes: [new SaveResultOutcome.Created(created), new SaveResultOutcome.AlreadyExists("m-5")]);
        store.GetOrCreate("m-5").GetOrCreatePlayer("u1").UpdateMetrics(10, null, null);

        var first = await svc.EndMatchAsync(EndRequest("m-5"));
        var second = await svc.EndMatchAsync(EndRequest("m-5"));

        Assert.IsType<SaveResultOutcome.Created>(first);
        Assert.IsType<SaveResultOutcome.AlreadyExists>(second);
        results.Verify(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EndMatch_Returns_Invalid_When_ServiceReportsInvalid()
    {
        var invalid = new SaveResultOutcome.Invalid(new Dictionary<string, string[]> { ["players"] = InvalidPlayerErrors });
        var (svc, store, _, _) = CreateService(outcomes: [invalid]);
        store.GetOrCreate("m-6").GetOrCreatePlayer("u1").UpdateMetrics(1, null, null);

        var outcome = await svc.EndMatchAsync(EndRequest("m-6"));

        var invalidOutcome = Assert.IsType<SaveResultOutcome.Invalid>(outcome);
        Assert.Contains("players", invalidOutcome.Errors.Keys);
    }
}
