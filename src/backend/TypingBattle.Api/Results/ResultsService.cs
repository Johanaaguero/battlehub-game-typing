using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TypingBattle.Api.Persistence;

namespace TypingBattle.Api.Results;

/// <summary>Resultado de intentar guardar una partida.</summary>
public abstract record SaveResultOutcome
{
    public sealed record Created(GameResultDto Result) : SaveResultOutcome;

    /// <summary>Ya existe un resultado para esa partida (solo se registra uno por <c>matchId</c>).</summary>
    public sealed record AlreadyExists(string MatchId) : SaveResultOutcome;

    public sealed record Invalid(IDictionary<string, string[]> Errors) : SaveResultOutcome;
}

/// <summary>
/// Casos de uso de resultados de Typing Battle. Lo usan los endpoints REST y, al terminar una partida,
/// el backend del hub <c>/hubs/typing</c> (04-persistencia-y-api-juegos.md: el juego registra su propio
/// resultado desde el backend, no desde el microfrontend). Se inyecta como servicio con alcance (scoped).
/// </summary>
public interface IResultsService
{
    Task<SaveResultOutcome> SaveAsync(SaveResultRequest request, CancellationToken cancellationToken = default);

    Task<GameResultDto?> GetAsync(string matchId, CancellationToken cancellationToken = default);

    /// <summary>Historial del jugador, del más reciente al más antiguo. <paramref name="limit"/> se limita a 1–200 (50 por defecto).</summary>
    Task<IReadOnlyList<PlayerHistoryItemDto>> GetHistoryAsync(
        string userId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default);

    /// <summary>Estadísticas agregadas. Un jugador sin partidas devuelve ceros, no un error.</summary>
    Task<PlayerStatsDto> GetStatsAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class ResultsService(TypingDbContext db, TimeProvider clock) : IResultsService
{
    public const int DefaultHistoryLimit = 50;
    public const int MaxHistoryLimit = 200;

    public async Task<SaveResultOutcome> SaveAsync(SaveResultRequest request, CancellationToken cancellationToken = default)
    {
        if (!ResultValidator.TryValidate(request, out var valid, out var errors))
        {
            return new SaveResultOutcome.Invalid(errors);
        }

        if (await db.GameResults.AnyAsync(g => g.MatchId == valid.MatchId, cancellationToken))
        {
            return new SaveResultOutcome.AlreadyExists(valid.MatchId);
        }

        var typingStats = TypingMetadata.ExtractPlayerStats(valid.Metadata);
        var entity = new GameResultEntity
        {
            MatchId = valid.MatchId,
            GameType = ResultValidator.GameType,
            StartedAt = valid.StartedAt,
            FinishedAt = valid.FinishedAt,
            WinnerUserId = valid.WinnerUserId,
            PlayersCount = valid.Players.Count,
            MetadataJson = valid.Metadata.GetRawText(),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            Players = valid.Players
                .Select((player, index) =>
                {
                    typingStats.TryGetValue(player.UserId, out var stats);
                    return new PlayerResultEntity
                    {
                        MatchId = valid.MatchId,
                        UserId = player.UserId,
                        DisplayName = player.DisplayName,
                        Score = player.Score,
                        Position = index + 1,
                        Wpm = stats?.Wpm,
                        Accuracy = stats?.Accuracy,
                    };
                })
                .ToList(),
        };

        db.GameResults.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dos solicitudes simultáneas para la misma partida: la segunda choca con la clave primaria.
            db.ChangeTracker.Clear();
            if (await db.GameResults.AsNoTracking().AnyAsync(g => g.MatchId == valid.MatchId, cancellationToken))
            {
                return new SaveResultOutcome.AlreadyExists(valid.MatchId);
            }

            throw;
        }

        return new SaveResultOutcome.Created(ToDto(entity));
    }

    public async Task<GameResultDto?> GetAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var entity = await db.GameResults
            .AsNoTracking()
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.MatchId == matchId, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<PlayerHistoryItemDto>> GetHistoryAsync(
        string userId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit ?? DefaultHistoryLimit, 1, MaxHistoryLimit);
        var skip = Math.Max(offset ?? 0, 0);

        return await (
                from player in db.PlayerResults.AsNoTracking()
                where player.UserId == userId
                join game in db.GameResults.AsNoTracking() on player.MatchId equals game.MatchId
                orderby game.FinishedAt descending, game.MatchId
                select new PlayerHistoryItemDto(
                    game.MatchId,
                    game.StartedAt,
                    game.FinishedAt,
                    player.Score,
                    player.Wpm,
                    player.Accuracy,
                    player.Position,
                    game.PlayersCount,
                    game.WinnerUserId == userId))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlayerStatsDto> GetStatsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var games = await (
                from player in db.PlayerResults.AsNoTracking()
                where player.UserId == userId
                join game in db.GameResults.AsNoTracking() on player.MatchId equals game.MatchId
                select new PlayerGameSample(
                    player.Score, player.Wpm, player.Accuracy, game.FinishedAt, game.WinnerUserId == userId))
            .ToListAsync(cancellationToken);

        return PlayerStatsCalculator.Calculate(userId, games);
    }

    private static GameResultDto ToDto(GameResultEntity entity)
    {
        using var metadata = JsonDocument.Parse(string.IsNullOrWhiteSpace(entity.MetadataJson) ? "{}" : entity.MetadataJson);

        return new GameResultDto(
            entity.MatchId,
            entity.GameType,
            entity.Players
                .OrderBy(p => p.Position)
                .Select(p => new PlayerResultDto(p.UserId, p.DisplayName, p.Score))
                .ToList(),
            entity.StartedAt,
            entity.FinishedAt,
            entity.WinnerUserId,
            metadata.RootElement.Clone());
    }
}
