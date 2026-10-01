using TypingBattle.Api.Results;

namespace TypingBattle.Api.Persistence;

/// <summary>Una partida jugada. La clave es <see cref="MatchId"/>: solo hay un resultado por partida.</summary>
public sealed class GameResultEntity
{
    public string MatchId { get; set; } = "";
    public string GameType { get; set; } = ResultValidator.GameType;

    /// <summary>Siempre en UTC (MySQL <c>datetime(6)</c> no guarda zona horaria).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Siempre en UTC.</summary>
    public DateTime FinishedAt { get; set; }

    public string? WinnerUserId { get; set; }

    /// <summary>Cantidad de jugadores; se guarda para no contarlos en cada consulta de historial.</summary>
    public int PlayersCount { get; set; }

    /// <summary>JSON libre del juego (contrato: <c>metadata</c>), en una columna <c>json</c>. Como mínimo es <c>{}</c>.</summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>Momento en que la API guardó el resultado (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    public List<PlayerResultEntity> Players { get; set; } = [];
}

/// <summary>Participación de un jugador en una partida.</summary>
public sealed class PlayerResultEntity
{
    public long Id { get; set; }
    public string MatchId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }

    /// <summary>Puesto final (1 = ganador). Coincide con el orden de <c>players</c> en la solicitud.</summary>
    public int Position { get; set; }

    // Copiados desde metadata.players[] para poder consultar historial y estadísticas en SQL.
    public double? Wpm { get; set; }
    public double? Accuracy { get; set; }
}
