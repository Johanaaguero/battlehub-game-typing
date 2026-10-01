using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TypingBattle.Api.Common;

namespace TypingBattle.Api.Results;

/// <summary>Resultado ya validado y normalizado (textos recortados, fechas en UTC).</summary>
public sealed record ValidResult(
    string MatchId,
    IReadOnlyList<PlayerResultDto> Players,
    DateTime StartedAt,
    DateTime FinishedAt,
    string? WinnerUserId,
    JsonElement Metadata);

/// <summary>Reglas de validación de <c>POST /api/games/typing/results</c>. Es lógica pura, sin dependencias.</summary>
public static class ResultValidator
{
    public const string GameType = "typing";
    public const int MaxIdLength = 200;
    public const int MaxPlayers = 50;
    public const int MaxMetadataChars = 16 * 1024;

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    /// <summary>
    /// Valida la solicitud. Si es válida devuelve el resultado normalizado; si no, un diccionario
    /// campo → mensajes (las claves usan camelCase, igual que el JSON).
    /// </summary>
    public static bool TryValidate(
        SaveResultRequest request,
        [NotNullWhen(true)] out ValidResult? valid,
        out Dictionary<string, string[]> errors)
    {
        var problems = new Dictionary<string, List<string>>();

        void Add(string key, string message)
        {
            if (!problems.TryGetValue(key, out var list))
            {
                problems[key] = list = [];
            }
            list.Add(message);
        }

        var matchId = request.MatchId?.Trim();
        if (string.IsNullOrEmpty(matchId))
        {
            Add("matchId", "Es obligatorio.");
        }
        else if (matchId.Length > MaxIdLength)
        {
            Add("matchId", $"No puede superar {MaxIdLength} caracteres.");
        }

        if (!string.Equals(request.GameType?.Trim(), GameType, StringComparison.OrdinalIgnoreCase))
        {
            Add("gameType", $"Debe ser '{GameType}'.");
        }

        var players = ValidatePlayers(request.Players, Add);
        var startedAt = ValidateDate("startedAt", request.StartedAt, Add);
        var finishedAt = ValidateDate("finishedAt", request.FinishedAt, Add);
        if (startedAt is { } start && finishedAt is { } end && end < start)
        {
            Add("finishedAt", "No puede ser anterior a startedAt.");
        }

        var winner = string.IsNullOrWhiteSpace(request.WinnerUserId) ? null : request.WinnerUserId.Trim();
        if (winner is not null && players is not null && !players.Any(p => p.UserId == winner))
        {
            Add("winnerUserId", "Debe ser uno de los jugadores de la partida.");
        }

        var metadata = ValidateMetadata(request.Metadata, Add);

        errors = problems.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        if (errors.Count > 0)
        {
            valid = null;
            return false;
        }

        valid = new ValidResult(matchId!, players!, startedAt!.Value, finishedAt!.Value, winner, metadata);
        return true;
    }

    private static List<PlayerResultDto>? ValidatePlayers(
        IReadOnlyList<PlayerResultRequest?>? input,
        Action<string, string> add)
    {
        if (input is null || input.Count == 0)
        {
            add("players", "Debe incluir al menos un jugador.");
            return null;
        }

        if (input.Count > MaxPlayers)
        {
            add("players", $"No puede incluir más de {MaxPlayers} jugadores.");
            return null;
        }

        var result = new List<PlayerResultDto>(input.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var allValid = true;

        for (var i = 0; i < input.Count; i++)
        {
            var key = $"players[{i}]";
            var player = input[i];
            if (player is null)
            {
                add(key, "No puede ser nulo.");
                allValid = false;
                continue;
            }

            var playerValid = true;
            var userId = player.UserId?.Trim();
            var displayName = player.DisplayName?.Trim();

            if (string.IsNullOrEmpty(userId))
            {
                add($"{key}.userId", "Es obligatorio.");
                playerValid = false;
            }
            else if (userId.Length > MaxIdLength)
            {
                add($"{key}.userId", $"No puede superar {MaxIdLength} caracteres.");
                playerValid = false;
            }
            else if (!seen.Add(userId))
            {
                add($"{key}.userId", "Jugador repetido.");
                playerValid = false;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                add($"{key}.displayName", "Es obligatorio.");
                playerValid = false;
            }
            else if (displayName.Length > MaxIdLength)
            {
                add($"{key}.displayName", $"No puede superar {MaxIdLength} caracteres.");
                playerValid = false;
            }

            if (player.Score is null or < 0)
            {
                add($"{key}.score", "Debe ser un entero mayor o igual a 0.");
                playerValid = false;
            }

            if (playerValid)
            {
                result.Add(new PlayerResultDto(userId!, displayName!, player.Score!.Value));
            }

            allValid &= playerValid;
        }

        return allValid ? result : null;
    }

    private static DateTime? ValidateDate(string key, DateTime? value, Action<string, string> add)
    {
        if (value is null)
        {
            add(key, "Es obligatoria.");
            return null;
        }

        if (!UtcDate.TryNormalize(value.Value, out var utc))
        {
            add(key, "Debe estar en UTC (ISO-8601 con sufijo Z, por ejemplo 2026-09-02T20:00:00Z).");
            return null;
        }

        return utc;
    }

    private static JsonElement ValidateMetadata(JsonElement? metadata, Action<string, string> add)
    {
        if (metadata is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return EmptyObject;
        }

        var element = metadata.Value;
        if (element.ValueKind != JsonValueKind.Object)
        {
            add("metadata", "Debe ser un objeto JSON.");
            return EmptyObject;
        }

        if (element.GetRawText().Length > MaxMetadataChars)
        {
            add("metadata", $"No puede superar {MaxMetadataChars} caracteres.");
            return EmptyObject;
        }

        return element.Clone();
    }
}
