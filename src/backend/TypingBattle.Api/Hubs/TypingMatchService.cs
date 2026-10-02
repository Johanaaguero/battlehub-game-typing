using System.Text.Json;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Hubs;

/// <summary>
/// Servicio que contiene la lógica de partida (facade) y usa TypingMatchStore para el estado compartido.
/// Responsable del lifecycle, progreso, métricas y finalizar partidas llamando a IResultsService.
/// La notificación a Matchmaking es desacoplada mediante IMatchFinishedNotifier (opcional, configurable).
/// </summary>
public sealed class TypingMatchService
{
    private readonly TypingMatchStore store;
    private readonly IResultsService resultsService;
    private readonly TimeProvider timeProvider;
    private readonly IMatchFinishedNotifier? notifier;

    public TypingMatchService(
        TypingMatchStore store,
        IResultsService resultsService,
        TimeProvider timeProvider,
        IMatchFinishedNotifier? notifier = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.resultsService = resultsService ?? throw new ArgumentNullException(nameof(resultsService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.notifier = notifier;
    }

    /// <summary>
    /// Intenta unir al jugador a la partida. Devuelve false si la partida no existe.
    /// </summary>
    public bool TryJoin(string matchId, string userId, string? displayName)
    {
        if (!store.TryGet(matchId, out var state)) return false;

        var player = state.GetOrCreatePlayer(userId, displayName);
        player.UpdateDisplayName(displayName);
        return true;
    }

    public bool TryLeave(string matchId, string userId)
    {
        if (!store.TryGet(matchId, out var state)) return false;
        state.RemovePlayer(userId);
        return true;
    }

    public bool TryUpdatePlayerMetrics(string matchId, string userId, int? score, double? wpm, double? accuracy)
    {
        if (!store.TryGet(matchId, out var state)) return false;
        var player = state.GetOrCreatePlayer(userId);
        player.UpdateMetrics(score, wpm, accuracy);
        return true;
    }

    /// <summary>
    /// Finaliza la partida: calcula el resultado, construye SaveResultRequest y llama a IResultsService.SaveAsync.
    /// Devuelve el SaveResultOutcome tal cual lo devuelve el servicio de resultados.
    /// </summary>
    public async Task<SaveResultOutcome> EndMatchAsync(EndMatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (!store.TryGet(request.MatchId, out var state))
        {
            // La partida no existe en el store: no somos dueños de crearla.
            // Devolvemos Invalid con error sencillo.
            return new SaveResultOutcome.Invalid(new Dictionary<string, string[]>
            {
                ["matchId"] = new[] { "Partida no encontrada." }
            });
        }

        // Lock per-match for deterministic finalization
        lock (state.SyncRoot)
        {
            // Mark finished timestamps if not provided
            var finished = request.FinishedAt ?? timeProvider.GetUtcNow().UtcDateTime;
            var started = request.StartedAt ?? state.StartedAt ?? finished;
            state.MarkStarted(started);
            state.MarkFinished(finished);
        }

        // Build players list for SaveResultRequest
        var players = state.Players
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.LastUpdated)
            .Select(p => new PlayerResultRequest(p.UserId, p.DisplayName, p.Score))
            .Cast<PlayerResultRequest?>()
            .ToList();

        // Determine winner: prefer explicit WinnerUserId, otherwise highest score.
        var winner = string.IsNullOrWhiteSpace(request.WinnerUserId)
            ? players.FirstOrDefault()?.UserId
            : request.WinnerUserId;

        // Build metadata JSON including typing metrics for each player to allow TypingMetadata.ExtractPlayerStats
        var metadataObj = new Dictionary<string, object?>
        {
            ["players"] = state.Players.Select(p => new
            {
                userId = p.UserId,
                wpm = p.Wpm,
                accuracy = p.Accuracy
            }).ToArray()
        };

        var metadataJson = JsonSerializer.Serialize(metadataObj);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson);
        var metadataElement = doc.RootElement.Clone();

        var saveRequest = new SaveResultRequest(
            MatchId: request.MatchId,
            GameType: ResultValidator.GameType,
            Players: players,
            StartedAt: request.StartedAt ?? state.StartedAt,
            FinishedAt: request.FinishedAt ?? state.FinishedAt,
            WinnerUserId: winner,
            Metadata: metadataElement);

        // Call existing results service
        var outcome = await resultsService.SaveAsync(saveRequest, cancellationToken);

        // Notify matchmaking if configured (decoupled)
        try
        {
            if (notifier is not null)
            {
                await notifier.NotifyMatchFinishedAsync(request.MatchId, outcome, cancellationToken);
            }
        }
        catch
        {
            // Notifier errors are non-fatal for result persistence; swallow after logging in future.
        }

        return outcome;
    }
}

/// <summary>
/// Abstracción para notificar a Matchmaking sobre el fin de una partida. La implementación concreta
/// se decide por el equipo (HTTP callback, message bus, SignalR a otro hub, etc.).
/// </summary>
public interface IMatchFinishedNotifier
{
    Task NotifyMatchFinishedAsync(string matchId, SaveResultOutcome outcome, CancellationToken cancellationToken = default);
}
