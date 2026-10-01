using System.Text.Json;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Results;

[Trait("Category", "Unit")]
public class TypingMetadataTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void LeeWpmYPrecisionPorJugador()
    {
        var metadata = Json("""
            {
              "textId": "t-07",
              "players": [
                { "userId": "user-001", "wpm": 62.4, "accuracy": 96.1, "finished": true },
                { "userId": "user-002", "wpm": 44 }
              ]
            }
            """);

        var stats = TypingMetadata.ExtractPlayerStats(metadata);

        Assert.Equal(2, stats.Count);
        Assert.Equal(new PlayerTypingStats(62.4, 96.1), stats["user-001"]);
        Assert.Equal(new PlayerTypingStats(44, null), stats["user-002"]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "players": {} }""")]
    [InlineData("""{ "players": "x" }""")]
    public void SinListaDeJugadores_DevuelveVacio(string json)
    {
        Assert.Empty(TypingMetadata.ExtractPlayerStats(Json(json)));
    }

    [Fact]
    public void IgnoraEntradasMalFormadas()
    {
        var metadata = Json("""
            {
              "players": [
                42,
                { "wpm": 50 },
                { "userId": 7, "wpm": 50 },
                { "userId": "  ", "wpm": 50 },
                { "userId": "ok", "wpm": "rápido", "accuracy": 90 }
              ]
            }
            """);

        var stats = TypingMetadata.ExtractPlayerStats(metadata);

        var only = Assert.Single(stats);
        Assert.Equal("ok", only.Key);
        Assert.Equal(new PlayerTypingStats(null, 90), only.Value);
    }
}
