using System.Text.Json;

namespace TypingBattle.Api.Hubs;

// --- Requests from clients ---
public sealed record JoinMatchRequest(string MatchId, string CurrentUser, string? DisplayName);

public sealed record LeaveMatchRequest(string MatchId, string CurrentUser);

public sealed record PlayerUpdateDto(
    string MatchId,
    string CurrentUser,
    int? Score,
    double? Wpm,
    double? Accuracy,
    DateTime? Timestamp);

/// <summary>Resumen de un jugador enviado al finalizar la partida por el cliente (o por el cliente+servidor).</summary>
public sealed record PlayerSummaryDto(string UserId, string? DisplayName, int Score);

/// <summary>Solicitud para terminar una partida. El servidor convertirá esto a SaveResultRequest
/// reutilizando PlayerResultRequest y ResultValidator.</summary>
public sealed record EndMatchRequest(
    string MatchId,
    string CurrentUser,
    IReadOnlyList<PlayerSummaryDto?>? Players,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? WinnerUserId,
    JsonElement? Metadata);

// --- Notifications from server to clients (group broadcasts) ---
public sealed record PlayerJoinedNotification(string UserId);
public sealed record PlayerLeftNotification(string UserId);
public sealed record PlayerUpdateNotification(string UserId, int? Score, double? Wpm, double? Accuracy);

/// <summary>Notificación de fin de partida; Outcome puede ser "created", "already_exists" o "invalid".</summary>
public sealed record MatchEndedNotification(string MatchId, string Outcome, object? ResultOrErrors);
