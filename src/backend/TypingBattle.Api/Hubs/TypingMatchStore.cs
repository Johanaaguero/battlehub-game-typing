using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace TypingBattle.Api.Hubs;

/// <summary>
/// Almacenamiento compartido de estado de partidas en memoria.
/// Thread-safe, pensado para registrarse como singleton e inyectarse en servicios/hubs.
/// No contiene lógica de negocio compleja; solo mantiene el estado mutables por partida.
/// </summary>
public sealed class TypingMatchStore
{
    private readonly ConcurrentDictionary<string, MatchState> _matches = new();

    public MatchState GetOrCreate(string matchId) => _matches.GetOrAdd(matchId, id => new MatchState(id));

    public bool TryGet(string matchId, [NotNullWhen(true)] out MatchState? state) => _matches.TryGetValue(matchId, out state);

    public bool TryRemove(string matchId, [NotNullWhen(true)] out MatchState? state) => _matches.TryRemove(matchId, out state);

    public IReadOnlyCollection<MatchState> ListAll() => _matches.Values.ToList();
}

/// <summary>Estado mutable de una partida. Contiene un locker para sincronizar operaciones compuestas.</summary>
public sealed class MatchState
{
    public string MatchId { get; }

    // Players keyed by userId
    private readonly ConcurrentDictionary<string, PlayerState> _players = new();

    /// <summary>Objeto para lock en operaciones compuestas que deban ser atómicas por partida.</summary>
    public object SyncRoot { get; } = new();

    public bool Ended { get; private set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    public MatchState(string matchId)
    {
        MatchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
    }

    public IEnumerable<PlayerState> Players => _players.Values;

    public PlayerState GetOrCreatePlayer(string userId, string? displayName = null)
    {
        return _players.GetOrAdd(userId, id => new PlayerState(id, displayName));
    }

    public bool TryGetPlayer(string userId, [NotNullWhen(true)] out PlayerState? player) => _players.TryGetValue(userId, out player);

    public void RemovePlayer(string userId) => _players.TryRemove(userId, out _);

    public void MarkStarted(DateTime started)
    {
        StartedAt = started;
    }

    public void MarkFinished(DateTime finished)
    {
        FinishedAt = finished;
        Ended = true;
    }
}

public sealed class PlayerState
{
    public string UserId { get; }
    public string? DisplayName { get; private set; }

    // Mutable metrics
    public int Score { get; private set; }
    public double? Wpm { get; private set; }
    public double? Accuracy { get; private set; }
    public DateTime LastUpdated { get; private set; }

    public PlayerState(string userId, string? displayName = null)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        DisplayName = displayName;
        LastUpdated = DateTime.UtcNow;
    }

    public void UpdateDisplayName(string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName)) DisplayName = displayName;
        LastUpdated = DateTime.UtcNow;
    }

    public void UpdateMetrics(int? score, double? wpm, double? accuracy)
    {
        if (score.HasValue) Score = score.Value;
        if (wpm.HasValue) Wpm = wpm.Value;
        if (accuracy.HasValue) Accuracy = accuracy.Value;
        LastUpdated = DateTime.UtcNow;
    }
}
