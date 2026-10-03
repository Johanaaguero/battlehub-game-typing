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
    /// Une al jugador a la partida. El primer jugador que entra crea el estado en memoria y fija el inicio:
    /// la sala la crea Matchmaking (dueño de su ciclo de vida) y el Shell solo carga el juego tras MatchStarted,
    /// así que este servicio nunca la conoce antes. Devuelve false si faltan los ids o si la partida ya terminó.
    /// </summary>
    public bool TryJoin(string matchId, string userId, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(userId)) return false;

        var state = store.GetOrCreate(matchId);
        lock (state.SyncRoot)
        {
            if (state.Ended) return false;
            if (state.StartedAt is null) state.MarkStarted(timeProvider.GetUtcNow().UtcDateTime);
        }

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

    public bool TryUpdatePlayerMetrics(string matchId, string userId, int? score, double? wpm, double? accuracy) =>
        TryUpdatePlayerMetrics(matchId, userId, score, wpm, accuracy, out _);

    /// <summary>
    /// Actualiza las métricas del jugador. Si el cliente no envía el puntaje, lo calcula el servidor con
    /// <see cref="TypingScore"/> a partir de la velocidad y la precisión vigentes.
    /// <paramref name="currentScore"/> devuelve el puntaje resultante, para retransmitirlo al grupo.
    /// </summary>
    public bool TryUpdatePlayerMetrics(
        string matchId, string userId, int? score, double? wpm, double? accuracy, out int currentScore)
    {
        currentScore = 0;
        if (!store.TryGet(matchId, out var state)) return false;

        var player = state.GetOrCreatePlayer(userId);
        var newWpm = wpm ?? player.Wpm;
        var newAccuracy = accuracy ?? player.Accuracy;
        var newScore = score ?? (newWpm is { } w && newAccuracy is { } a ? TypingScore.Calculate(w, a) : null);

        player.UpdateMetrics(newScore, wpm, accuracy);
        currentScore = player.Score;
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
            // Sin nombre visible se usa el id: un jugador sin nombre haría inválido el resultado de toda la partida.
            .Select(p => new PlayerResultRequest(p.UserId, string.IsNullOrWhiteSpace(p.DisplayName) ? p.UserId : p.DisplayName, p.Score))
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
