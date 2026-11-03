using TypingBattle.Api.Hubs;

namespace TypingBattle.UnitTests.Hubs;

/// <summary>El estado de las partidas en curso vive en memoria (<see cref="TypingMatchStore"/>); aquí no hay nada que simular.</summary>
[Trait("Category", "Unit")]
public class TypingMatchStoreTests
{
    [Fact]
    public void GetOrCreate_ReturnsTheSameMatch_ForTheSameId()
    {
        var store = new TypingMatchStore();

        var first = store.GetOrCreate("m-1");
        var second = store.GetOrCreate("m-1");

        Assert.Same(first, second);
        Assert.Equal("m-1", first.MatchId);
        Assert.Single(store.ListAll());
    }

    [Fact]
    public void TryGet_FindsOnlyMatchesThatWereCreated()
    {
        var store = new TypingMatchStore();
        store.GetOrCreate("m-1");

        Assert.True(store.TryGet("m-1", out var found));
        Assert.Equal("m-1", found.MatchId);
        Assert.False(store.TryGet("m-x", out _));
    }

    [Fact]
    public void TryRemove_DeletesTheMatch()
    {
        var store = new TypingMatchStore();
        store.GetOrCreate("m-1");

        Assert.True(store.TryRemove("m-1", out var removed));
        Assert.Equal("m-1", removed.MatchId);
        Assert.False(store.TryGet("m-1", out _));
        Assert.False(store.TryRemove("m-1", out _));
    }

    [Fact]
    public void ListAll_ReturnsASnapshot_ThatLaterChangesDoNotAlter()
    {
        var store = new TypingMatchStore();
        store.GetOrCreate("m-1");

        var snapshot = store.ListAll();
        store.GetOrCreate("m-2");

        Assert.Single(snapshot);
        Assert.Equal(2, store.ListAll().Count);
    }

    [Fact]
    public void Match_RejectsAMissingId() =>
        Assert.Throws<ArgumentNullException>(() => new MatchState(null!));

    [Fact]
    public void Match_IsNotEnded_UntilItIsMarkedFinished()
    {
        var match = new MatchState("m-1");
        var started = new DateTime(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc);
        var finished = started.AddSeconds(60);

        Assert.False(match.Ended);
        Assert.Null(match.StartedAt);

        match.MarkStarted(started);
        Assert.False(match.Ended);
        Assert.Equal(started, match.StartedAt);

        match.MarkFinished(finished);
        Assert.True(match.Ended);
        Assert.Equal(finished, match.FinishedAt);
    }

    [Fact]
    public void Match_KeepsTheFirstNameOfAPlayer_AndRemovesPlayers()
    {
        var match = new MatchState("m-1");

        var first = match.GetOrCreatePlayer("u1", "Ana");
        var again = match.GetOrCreatePlayer("u1", "Otro nombre");

        Assert.Same(first, again);
        Assert.Equal("Ana", again.DisplayName);
        Assert.Single(match.Players);

        match.RemovePlayer("u1");
        match.RemovePlayer("u-que-no-esta");

        Assert.Empty(match.Players);
        Assert.False(match.TryGetPlayer("u1", out _));
    }

    [Fact]
    public void Player_RejectsAMissingId() =>
        Assert.Throws<ArgumentNullException>(() => new PlayerState(null!));

    [Fact]
    public void Player_UpdateMetrics_ChangesOnlyTheValuesThatWereSent()
    {
        var player = new PlayerState("u1", "Ana");

        player.UpdateMetrics(100, 50.0, 90.0);
        player.UpdateMetrics(null, 60.0, null);

        Assert.Equal(100, player.Score);
        Assert.Equal(60.0, player.Wpm);
        Assert.Equal(90.0, player.Accuracy);
    }

    [Theory]
    [InlineData("Luis", "Luis")]
    [InlineData("", "Ana")]
    [InlineData("  ", "Ana")]
    [InlineData(null, "Ana")]
    public void Player_UpdateDisplayName_IgnoresABlankName(string? newName, string expected)
    {
        var player = new PlayerState("u1", "Ana");

        player.UpdateDisplayName(newName);

        Assert.Equal(expected, player.DisplayName);
    }
}
