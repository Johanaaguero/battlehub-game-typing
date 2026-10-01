using System.Text.Json;

namespace TypingBattle.Api.Results;

// Los campos de las solicitudes son anulables a propósito: reflejan lo que puede llegar en el JSON
// y el validador (ResultValidator) responde con errores claros en lugar de lanzar excepciones.

/// <summary>Jugador dentro de la solicitud <c>POST /results</c>. La posición en la lista es el ranking final (1.º = ganador).</summary>
public sealed record PlayerResultRequest(string? UserId, string? DisplayName, int? Score);

/// <summary>Cuerpo de <c>POST /api/games/typing/results</c> (forma definida en 04-persistencia-y-api-juegos.md).</summary>
public sealed record SaveResultRequest(
    string? MatchId,
    string? GameType,
    IReadOnlyList<PlayerResultRequest?>? Players,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? WinnerUserId,
    JsonElement? Metadata);

/// <summary>Jugador en las respuestas de la API.</summary>
public sealed record PlayerResultDto(string UserId, string DisplayName, int Score);

/// <summary>Resultado de una partida. Las fechas salen siempre en UTC con sufijo Z.</summary>
public sealed record GameResultDto(
    string MatchId,
    string GameType,
    IReadOnlyList<PlayerResultDto> Players,
    DateTime StartedAt,
    DateTime FinishedAt,
    string? WinnerUserId,
    JsonElement Metadata);

/// <summary>Una fila del historial de un jugador (<c>GET /players/{userId}/history</c>).</summary>
public sealed record PlayerHistoryItemDto(
    string MatchId,
    DateTime StartedAt,
    DateTime FinishedAt,
    int Score,
    double? Wpm,
    double? Accuracy,
    int Position,
    int PlayersCount,
    bool Won);

/// <summary>Estadísticas agregadas de un jugador (<c>GET /players/{userId}/stats</c>).</summary>
public sealed record PlayerStatsDto(
    string UserId,
    int GamesPlayed,
    int Wins,
    double WinRate,
    double AverageScore,
    int BestScore,
    double? AverageWpm,
    double? BestWpm,
    double? AverageAccuracy,
    DateTime? LastPlayedAt);
