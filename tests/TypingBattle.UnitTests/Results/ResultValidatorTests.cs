using System.Text.Json;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Results;

[Trait("Category", "Unit")]
public class ResultValidatorTests
{
    private static readonly DateTime Start = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddSeconds(47);

    private static SaveResultRequest ValidRequest() => new(
        MatchId: "match-001",
        GameType: "typing",
        Players:
        [
            new PlayerResultRequest("user-001", "Ana", 850),
            new PlayerResultRequest("user-002", "Luis", 620),
        ],
        StartedAt: Start,
        FinishedAt: End,
        WinnerUserId: "user-001",
        Metadata: JsonDocument.Parse("""{ "textId": "t-07" }""").RootElement);

    [Fact]
    public void SolicitudValida_DevuelveResultadoNormalizado()
    {
        var request = ValidRequest() with { MatchId = "  match-001  ", GameType = "TYPING" };

        var ok = ResultValidator.TryValidate(request, out var valid, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.NotNull(valid);
        Assert.Equal("match-001", valid.MatchId);
        Assert.Equal(2, valid.Players.Count);
        Assert.Equal("t-07", valid.Metadata.GetProperty("textId").GetString());
    }

    [Fact]
    public void SinMetadata_UsaObjetoVacio()
    {
        var ok = ResultValidator.TryValidate(ValidRequest() with { Metadata = null }, out var valid, out _);

        Assert.True(ok);
        Assert.Equal(JsonValueKind.Object, valid!.Metadata.ValueKind);
        Assert.Equal("{}", valid.Metadata.GetRawText());
    }

    [Fact]
    public void SinGanador_EsValido()
    {
        var ok = ResultValidator.TryValidate(ValidRequest() with { WinnerUserId = "  " }, out var valid, out _);

        Assert.True(ok);
        Assert.Null(valid!.WinnerUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MatchIdVacio_EsError(string? matchId)
    {
        AssertInvalid(ValidRequest() with { MatchId = matchId }, "matchId");
    }

    [Fact]
    public void MatchIdDemasiadoLargo_EsError()
    {
        AssertInvalid(ValidRequest() with { MatchId = new string('x', ResultValidator.MaxIdLength + 1) }, "matchId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("trivia")]
    public void GameTypeDistintoDeTyping_EsError(string? gameType)
    {
        AssertInvalid(ValidRequest() with { GameType = gameType }, "gameType");
    }

    [Fact]
    public void SinJugadores_EsError()
    {
        AssertInvalid(ValidRequest() with { Players = [] }, "players");
        AssertInvalid(ValidRequest() with { Players = null }, "players");
    }

    [Fact]
    public void DemasiadosJugadores_EsError()
    {
        var players = Enumerable.Range(0, ResultValidator.MaxPlayers + 1)
            .Select(i => (PlayerResultRequest?)new PlayerResultRequest($"u{i}", $"U{i}", 1))
            .ToList();

        AssertInvalid(ValidRequest() with { Players = players, WinnerUserId = null }, "players");
    }

    [Fact]
    public void JugadorRepetido_EsError()
    {
        var request = ValidRequest() with
        {
            Players = [new PlayerResultRequest("user-001", "Ana", 1), new PlayerResultRequest("user-001", "Ana 2", 2)],
        };

        AssertInvalid(request, "players[1].userId");
    }

    [Fact]
    public void JugadorIncompleto_ReportaCadaCampo()
    {
        var request = ValidRequest() with
        {
            Players = [new PlayerResultRequest(null, " ", -5), null],
            WinnerUserId = null,
        };

        var ok = ResultValidator.TryValidate(request, out _, out var errors);

        Assert.False(ok);
        Assert.Contains("players[0].userId", errors.Keys);
        Assert.Contains("players[0].displayName", errors.Keys);
        Assert.Contains("players[0].score", errors.Keys);
        Assert.Contains("players[1]", errors.Keys);
    }

    [Fact]
    public void FechasFaltantes_SonError()
    {
        var ok = ResultValidator.TryValidate(ValidRequest() with { StartedAt = null, FinishedAt = null }, out _, out var errors);

        Assert.False(ok);
        Assert.Contains("startedAt", errors.Keys);
        Assert.Contains("finishedAt", errors.Keys);
    }

    [Fact]
    public void FechaSinZona_EsError()
    {
        var unspecified = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);

        AssertInvalid(ValidRequest() with { StartedAt = unspecified }, "startedAt");
    }

    [Fact]
    public void FinAnteriorAlInicio_EsError()
    {
        AssertInvalid(ValidRequest() with { FinishedAt = Start.AddSeconds(-1) }, "finishedAt");
    }

    [Fact]
    public void GanadorQueNoJugo_EsError()
    {
        AssertInvalid(ValidRequest() with { WinnerUserId = "user-999" }, "winnerUserId");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"texto\"")]
    [InlineData("42")]
    public void MetadataQueNoEsObjeto_EsError(string json)
    {
        AssertInvalid(ValidRequest() with { Metadata = JsonDocument.Parse(json).RootElement }, "metadata");
    }

    [Fact]
    public void MetadataDemasiadoGrande_EsError()
    {
        var big = JsonSerializer.Serialize(new { text = new string('a', ResultValidator.MaxMetadataChars) });

        AssertInvalid(ValidRequest() with { Metadata = JsonDocument.Parse(big).RootElement }, "metadata");
    }

    private static void AssertInvalid(SaveResultRequest request, string expectedKey)
    {
        var ok = ResultValidator.TryValidate(request, out var valid, out var errors);

        Assert.False(ok);
        Assert.Null(valid);
        Assert.Contains(expectedKey, errors.Keys);
    }
}
