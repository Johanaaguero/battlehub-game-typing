using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Results;

[Trait("Category", "Unit")]
public class PlayerStatsCalculatorTests
{
    private static readonly DateTime Day = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SinPartidas_DevuelveCerosYNulos()
    {
        var stats = PlayerStatsCalculator.Calculate("ana", []);

        Assert.Equal(new PlayerStatsDto("ana", 0, 0, 0, 0, 0, null, null, null, null), stats);
    }

    [Fact]
    public void AgregaPromediosMejoresYVictorias()
    {
        PlayerGameSample[] games =
        [
            new(300, 30.0, 90.0, Day, Won: false),
            new(600, 60.0, 100.0, Day.AddHours(2), Won: true),
            new(450, 45.0, 95.0, Day.AddHours(1), Won: false),
        ];

        var stats = PlayerStatsCalculator.Calculate("ana", games);

        Assert.Equal("ana", stats.UserId);
        Assert.Equal(3, stats.GamesPlayed);
        Assert.Equal(1, stats.Wins);
        Assert.Equal(0.333, stats.WinRate);
        Assert.Equal(450.0, stats.AverageScore);
        Assert.Equal(600, stats.BestScore);
        Assert.Equal(45.0, stats.AverageWpm);
        Assert.Equal(60.0, stats.BestWpm);
        Assert.Equal(95.0, stats.AverageAccuracy);
        Assert.Equal(Day.AddHours(2), stats.LastPlayedAt);
    }

    [Fact]
    public void PartidasSinMetricas_NoCuentanEnLosPromediosDeWpm()
    {
        PlayerGameSample[] games =
        [
            new(100, null, null, Day, Won: true),
            new(200, 40.26, null, Day.AddMinutes(5), Won: true),
        ];

        var stats = PlayerStatsCalculator.Calculate("ana", games);

        Assert.Equal(1.0, stats.WinRate);
        Assert.Equal(150.0, stats.AverageScore);
        Assert.Equal(40.3, stats.AverageWpm);
        Assert.Equal(40.26, stats.BestWpm);
        Assert.Null(stats.AverageAccuracy);
    }
}
