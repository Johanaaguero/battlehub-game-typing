using System.Text.Json;

namespace TypingBattle.Api.Results;

/// <summary>Estadísticas de mecanografía de un jugador dentro de una partida.</summary>
public sealed record PlayerTypingStats(double? Wpm, double? Accuracy);

/// <summary>
/// Lee del <c>metadata</c> libre de una partida los datos propios de Typing Battle que se
/// necesitan para agregar estadísticas: <c>metadata.players[] = { userId, wpm, accuracy }</c>.
/// El esquema completo está en docs/api-resultados.md.
/// </summary>
public static class TypingMetadata
{
    public static IReadOnlyDictionary<string, PlayerTypingStats> ExtractPlayerStats(JsonElement metadata)
    {
        var result = new Dictionary<string, PlayerTypingStats>(StringComparer.Ordinal);

        if (metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("players", out var players)
            || players.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var player in players.EnumerateArray())
        {
            if (player.ValueKind != JsonValueKind.Object
                || !player.TryGetProperty("userId", out var id)
                || id.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var userId = id.GetString()?.Trim();
            if (string.IsNullOrEmpty(userId))
            {
                continue;
            }

            result[userId] = new PlayerTypingStats(ReadNumber(player, "wpm"), ReadNumber(player, "accuracy"));
        }

        return result;
    }

    private static double? ReadNumber(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;
}
