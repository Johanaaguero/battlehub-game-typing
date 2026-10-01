using Microsoft.AspNetCore.Http.HttpResults;

namespace TypingBattle.Api.Results;

/// <summary>
/// API REST mínima obligatoria del juego (04-persistencia-y-api-juegos.md):
/// registrar un resultado, consultarlo, historial y estadísticas de un jugador.
/// </summary>
public static class ResultsEndpoints
{
    public const string BasePath = "/api/games/typing";

    public static RouteGroupBuilder MapResultsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath).WithTags("Typing Battle · resultados");

        group.MapPost("/results", SaveResult)
            .WithName("SaveTypingResult")
            .WithSummary("Registra el resultado de una partida finalizada. Solo se acepta un resultado por matchId.");

        group.MapGet("/results/{matchId}", GetResult)
            .WithName("GetTypingResult")
            .WithSummary("Obtiene el resultado detallado de una partida.");

        group.MapGet("/players/{userId}/history", GetHistory)
            .WithName("GetTypingPlayerHistory")
            .WithSummary("Historial de partidas de un jugador, de la más reciente a la más antigua (limit 1–200, por defecto 50).");

        group.MapGet("/players/{userId}/stats", GetStats)
            .WithName("GetTypingPlayerStats")
            .WithSummary("Estadísticas agregadas de un jugador (promedios, mejores marcas, victorias).");

        return group;
    }

    private static async Task<Results<Created<GameResultDto>, ValidationProblem, ProblemHttpResult>> SaveResult(
        SaveResultRequest request, IResultsService service, CancellationToken cancellationToken)
    {
        var outcome = await service.SaveAsync(request, cancellationToken);

        return outcome switch
        {
            SaveResultOutcome.Created created => TypedResults.Created(
                $"{BasePath}/results/{Uri.EscapeDataString(created.Result.MatchId)}", created.Result),
            SaveResultOutcome.Invalid invalid => TypedResults.ValidationProblem(invalid.Errors),
            SaveResultOutcome.AlreadyExists existing => TypedResults.Problem(
                title: "El resultado ya existe",
                detail: $"Ya hay un resultado registrado para la partida '{existing.MatchId}'.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Resultado inesperado: {outcome.GetType().Name}"),
        };
    }

    private static async Task<Results<Ok<GameResultDto>, NotFound>> GetResult(
        string matchId, IResultsService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(matchId, cancellationToken);
        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    private static async Task<Ok<IReadOnlyList<PlayerHistoryItemDto>>> GetHistory(
        string userId, int? limit, int? offset, IResultsService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetHistoryAsync(userId, limit, offset, cancellationToken));

    private static async Task<Ok<PlayerStatsDto>> GetStats(
        string userId, IResultsService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetStatsAsync(userId, cancellationToken));
}
