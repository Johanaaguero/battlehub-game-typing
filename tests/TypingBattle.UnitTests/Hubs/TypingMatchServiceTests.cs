using System.Text.Json;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Hubs;

[Trait("Category", "Unit")]
public class TypingMatchServiceTests
{
    private static TimeProvider UtcTimeProvider() => TimeProvider.System;

    private static TypingMatchStore CreateStoreWithMatch(string matchId)
    {
        var store = new TypingMatchStore();
        var m = store.GetOrCreate(matchId);
        return store;
    }

    private sealed class FakeResultsService : IResultsService
    {
        private readonly Queue<SaveResultOutcome> responses = new();
        public SaveResultRequest? LastRequest { get; private set; }

        public FakeResultsService(params SaveResultOutcome[] responses)
        {
            foreach (var r in responses) this.responses.Enqueue(r);
        }

        public Task<SaveResultOutcome> SaveAsync(SaveResultRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (responses.Count > 0)
            {
                return Task.FromResult(responses.Dequeue());
            }

            // Default: return Created echoing back a simple GameResultDto
            var players = request.Players!.Select(p => new PlayerResultDto(p.UserId, p.DisplayName ?? string.Empty, p.Score ?? 0)).ToList();
            var dto = new GameResultDto(request.MatchId!, request.GameType!, players, request.StartedAt!.Value, request.FinishedAt!.Value, request.WinnerUserId, request.Metadata ?? JsonDocument.Parse("{}").RootElement);
            return Task.FromResult<SaveResultOutcome>(new SaveResultOutcome.Created(dto));
        }

        public Task<GameResultDto?> GetAsync(string matchId, CancellationToken cancellationToken = default) => Task.FromResult<GameResultDto?>(null);
        public Task<IReadOnlyList<PlayerHistoryItemDto>> GetHistoryAsync(string userId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlayerHistoryItemDto>>(Array.Empty<PlayerHistoryItemDto>());
        public Task<PlayerStatsDto> GetStatsAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(new PlayerStatsDto(userId,0,0,0,0,0,null,null,null,null));
    }

    [Fact]
    public void JoinMatch_Succeeds_WhenMatchExists()
    {
        var store = CreateStoreWithMatch("m-1");
        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        var ok = svc.TryJoin("m-1", "u1", "Ana");

        Assert.True(ok);
        Assert.True(store.TryGet("m-1", out var state));
        Assert.True(state.TryGetPlayer("u1", out var player));
        Assert.Equal("Ana", player.DisplayName);
    }

    [Fact]
    public void JoinMatch_Fails_WhenMatchNotExist()
    {
        var store = new TypingMatchStore();
        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        var ok = svc.TryJoin("m-unknown", "u1", "Ana");

        Assert.False(ok);
    }

    [Fact]
    public void LeaveMatch_RemovesPlayer()
    {
        var store = CreateStoreWithMatch("m-2");
        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        svc.TryJoin("m-2", "u1", "Ana");
        var left = svc.TryLeave("m-2", "u1");

        Assert.True(left);
        Assert.True(store.TryGet("m-2", out var state));
        Assert.False(state.TryGetPlayer("u1", out _));
    }

    [Fact]
    public void UpdateMetrics_CreatesPlayer_WhenAbsent()
    {
        var store = CreateStoreWithMatch("m-3");
        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

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
        var store = new TypingMatchStore();
        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        var ok = svc.TryUpdatePlayerMetrics("m-x", "u1", 1, null, null);

        Assert.False(ok);
    }

    [Fact]
    public async Task EndMatch_Builds_SaveRequest_And_Returns_Created()
    {
        var store = CreateStoreWithMatch("m-4");
        // populate players
        var state = store.GetOrCreate("m-4");
        var p1 = state.GetOrCreatePlayer("u1", "Ana");
        p1.UpdateMetrics(300, 30.0, 90.0);
        var p2 = state.GetOrCreatePlayer("u2", "Luis");
        p2.UpdateMetrics(600, 60.0, 100.0);

        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        var req = new EndMatchRequest("m-4", "u1", null, null, null, null, null);
        var outcome = await svc.EndMatchAsync(req);

        Assert.IsType<SaveResultOutcome.Created>(outcome);
        Assert.NotNull(fake.LastRequest);
        Assert.Equal("m-4", fake.LastRequest!.MatchId);
        Assert.Equal(ResultValidator.GameType, fake.LastRequest.GameType);
        Assert.Equal(2, fake.LastRequest.Players!.Count);
        // metadata contains players with wpm/accuracy
        var meta = fake.LastRequest.Metadata;
        Assert.True(meta.HasValue);
        Assert.Equal(JsonValueKind.Object, meta.Value.ValueKind);
        Assert.True(meta.Value.TryGetProperty("players", out var playersArr));
        Assert.Equal(2, playersArr.GetArrayLength());
    }

    [Fact]
    public async Task EndMatch_Returns_AlreadyExists_On_Second_Call()
    {
        var store = CreateStoreWithMatch("m-5");
        var state = store.GetOrCreate("m-5");
        state.GetOrCreatePlayer("u1").UpdateMetrics(10, null, null);

        // Prepare fake to return Created then AlreadyExists
        var createdDto = new GameResultDto("m-5", ResultValidator.GameType, new List<PlayerResultDto> { new PlayerResultDto("u1","u1",10)}, DateTime.UtcNow, DateTime.UtcNow, "u1", JsonDocument.Parse("{}").RootElement);
        var fake = new FakeResultsService(new SaveResultOutcome.Created(createdDto), new SaveResultOutcome.AlreadyExists("m-5"));
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        var req = new EndMatchRequest("m-5", "u1", null, null, null, null, null);
        var first = await svc.EndMatchAsync(req);
        var second = await svc.EndMatchAsync(req);

        Assert.IsType<SaveResultOutcome.Created>(first);
        Assert.IsType<SaveResultOutcome.AlreadyExists>(second);
    }

    [Fact]
    public async Task EndMatch_Returns_Invalid_When_ServiceReportsInvalid()
    {
        var store = CreateStoreWithMatch("m-6");
        var state = store.GetOrCreate("m-6");
        state.GetOrCreatePlayer("u1").UpdateMetrics(1, null, null);

        var invalid = new SaveResultOutcome.Invalid(new Dictionary<string, string[]> { ["players"] = new[] { "error" } });
        var fake = new FakeResultsService(invalid);
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        var req = new EndMatchRequest("m-6", "u1", null, null, null, null, null);
        var outcome = await svc.EndMatchAsync(req);

        Assert.IsType<SaveResultOutcome.Invalid>(outcome);
        var invalidOutcome = (SaveResultOutcome.Invalid)outcome;
        Assert.Contains("players", invalidOutcome.Errors.Keys);
    }

    [Fact]
    public void WpmAndAccuracy_Are_NotCalculated_ServerSide()
    {
        // The service stores and forwards WPM/accuracy provided by clients; it does not compute them.
        var store = CreateStoreWithMatch("m-7");
        var fake = new FakeResultsService();
        var svc = new TypingMatchService(store, fake, UtcTimeProvider());

        svc.TryUpdatePlayerMetrics("m-7", "u1", 42, 55.5, 99.9);

        var state = store.GetOrCreate("m-7");
        var p = state.GetOrCreatePlayer("u1");
        Assert.Equal(55.5, p.Wpm);
        Assert.Equal(99.9, p.Accuracy);
    }
}
