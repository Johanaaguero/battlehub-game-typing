namespace TypingBattle.Api.Results;

/// <summary>Una partida de un jugador, con lo mínimo necesario para calcular sus estadísticas.</summary>
public sealed record PlayerGameSample(int Score, double? Wpm, double? Accuracy, DateTime FinishedAt, bool Won);

/// <summary>Cálculo de las estadísticas agregadas de un jugador. Es lógica pura, sin dependencias.</summary>
public static class PlayerStatsCalculator
{
    /// <summary>
    /// Agrega las partidas del jugador. Sin partidas devuelve ceros y <c>null</c> en los promedios.
    /// <c>winRate</c> es una fracción entre 0 y 1 (3 decimales); los promedios llevan 1 decimal.
    /// </summary>
    public static PlayerStatsDto Calculate(string userId, IReadOnlyCollection<PlayerGameSample> games)
    {
        if (games.Count == 0)
        {
            return new PlayerStatsDto(userId, 0, 0, 0, 0, 0, null, null, null, null);
        }

        var wins = games.Count(g => g.Won);
        var wpms = games.Where(g => g.Wpm.HasValue).Select(g => g.Wpm!.Value).ToList();
        var accuracies = games.Where(g => g.Accuracy.HasValue).Select(g => g.Accuracy!.Value).ToList();

        return new PlayerStatsDto(
            userId,
            GamesPlayed: games.Count,
            Wins: wins,
            WinRate: Math.Round(wins / (double)games.Count, 3),
            AverageScore: Math.Round(games.Average(g => g.Score), 1),
            BestScore: games.Max(g => g.Score),
            AverageWpm: wpms.Count > 0 ? Math.Round(wpms.Average(), 1) : null,
            BestWpm: wpms.Count > 0 ? wpms.Max() : null,
            AverageAccuracy: accuracies.Count > 0 ? Math.Round(accuracies.Average(), 1) : null,
            LastPlayedAt: games.Max(g => g.FinishedAt));
    }
}
