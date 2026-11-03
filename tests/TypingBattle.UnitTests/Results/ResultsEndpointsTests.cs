using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Results;

/// <summary>
/// Los manejadores de la API REST se prueban llamándolos directamente, con el servicio de resultados (MySQL) simulado con Moq:
/// aquí se comprueba qué respuesta HTTP sale de cada resultado del servicio. Las rutas y MySQL reales se prueban en integración.
/// </summary>
public abstract class ResultsEndpointsTestBase
{
    protected static readonly DateTime Started = new(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc);

    protected readonly Mock<IResultsService> Service = new();

    protected static GameResultDto Result(string matchId = "match-001") => new(
        matchId,
        ResultValidator.GameType,
        [new PlayerResultDto("ana", "Ana", 600)],
        Started,
        Started.AddSeconds(60),
        "ana",
        JsonDocument.Parse("{}").RootElement);

    protected static SaveResultRequest Request() => new(
        "match-001", ResultValidator.GameType, [new PlayerResultRequest("ana", "Ana", 600)], Started, Started.AddSeconds(60), "ana", null);

    protected void SaveReturns(SaveResultOutcome outcome) =>
        Service
            .Setup(s => s.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
}

[Trait("Category", "Unit")]
public class ResultsEndpointsSaveResultTests : ResultsEndpointsTestBase
{
    private sealed record UnknownOutcome : SaveResultOutcome;

    [Fact]
    public async Task Created_Responds201_WithTheLocationOfTheResult()
    {
        var result = Result("match 001");
        SaveReturns(new SaveResultOutcome.Created(result));

        var response = await ResultsEndpoints.SaveResult(Request(), Service.Object, CancellationToken.None);

        var created = Assert.IsType<Created<GameResultDto>>(response.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal("/api/games/typing/results/match%20001", created.Location);
        Assert.Same(result, created.Value);
    }

    [Fact]
    public async Task Invalid_Responds400_WithTheErrorsByField()
    {
        var errors = new Dictionary<string, string[]> { ["gameType"] = ["Debe ser 'typing'."] };
        SaveReturns(new SaveResultOutcome.Invalid(errors));

        var response = await ResultsEndpoints.SaveResult(Request(), Service.Object, CancellationToken.None);

        var problem = Assert.IsType<ValidationProblem>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(["Debe ser 'typing'."], problem.ProblemDetails.Errors["gameType"]);
    }

    [Fact]
    public async Task AlreadyExists_Responds409_NamingTheMatch()
    {
        SaveReturns(new SaveResultOutcome.AlreadyExists("match-001"));

        var response = await ResultsEndpoints.SaveResult(Request(), Service.Object, CancellationToken.None);

        var conflict = Assert.IsType<ProblemHttpResult>(response.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("El resultado ya existe", conflict.ProblemDetails.Title);
        Assert.Contains("match-001", conflict.ProblemDetails.Detail);
    }

    [Fact]
    public async Task AnUnknownOutcome_IsAProgrammingError()
    {
        SaveReturns(new UnknownOutcome());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResultsEndpoints.SaveResult(Request(), Service.Object, CancellationToken.None));
    }

    [Fact]
    public async Task SendsTheRequestAndTheCancellationToken_ToTheService()
    {
        SaveReturns(new SaveResultOutcome.Created(Result()));
        var request = Request();
        using var cts = new CancellationTokenSource();

        await ResultsEndpoints.SaveResult(request, Service.Object, cts.Token);

        Service.Verify(s => s.SaveAsync(request, cts.Token), Times.Once);
    }
}

[Trait("Category", "Unit")]
public class ResultsEndpointsGetResultTests : ResultsEndpointsTestBase
{
    [Fact]
    public async Task Found_Responds200_WithTheResult()
    {
        var result = Result();
        Service.Setup(s => s.GetAsync("match-001", It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var response = await ResultsEndpoints.GetResult("match-001", Service.Object, CancellationToken.None);

        var ok = Assert.IsType<Ok<GameResultDto>>(response.Result);
        Assert.Same(result, ok.Value);
    }

    [Fact]
    public async Task NotFound_Responds404()
    {
        Service.Setup(s => s.GetAsync("match-x", It.IsAny<CancellationToken>())).ReturnsAsync((GameResultDto?)null);

        var response = await ResultsEndpoints.GetResult("match-x", Service.Object, CancellationToken.None);

        Assert.IsType<NotFound>(response.Result);
    }
}

[Trait("Category", "Unit")]
public class ResultsEndpointsGetHistoryTests : ResultsEndpointsTestBase
{
    [Fact]
    public async Task Responds200_WithTheHistory_AndPassesLimitAndOffset()
    {
        IReadOnlyList<PlayerHistoryItemDto> history =
            [new("match-001", Started, Started.AddSeconds(60), 600, 60.0, 100.0, 1, 2, true)];
        Service.Setup(s => s.GetHistoryAsync("auth0|ana", 10, 20, It.IsAny<CancellationToken>())).ReturnsAsync(history);

        var response = await ResultsEndpoints.GetHistory("auth0|ana", 10, 20, Service.Object, CancellationToken.None);

        Assert.Same(history, response.Value);
        Service.Verify(s => s.GetHistoryAsync("auth0|ana", 10, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithoutLimitOrOffset_LeavesTheDefaultsToTheService()
    {
        Service
            .Setup(s => s.GetHistoryAsync("ana", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await ResultsEndpoints.GetHistory("ana", null, null, Service.Object, CancellationToken.None);

        Assert.Empty(response.Value!);
    }
}

[Trait("Category", "Unit")]
public class ResultsEndpointsGetStatsTests : ResultsEndpointsTestBase
{
    [Fact]
    public async Task Responds200_WithTheStatistics()
    {
        var stats = new PlayerStatsDto("ana", 3, 2, 0.667, 450, 600, 45, 60, 95, Started);
        Service.Setup(s => s.GetStatsAsync("ana", It.IsAny<CancellationToken>())).ReturnsAsync(stats);

        var response = await ResultsEndpoints.GetStats("ana", Service.Object, CancellationToken.None);

        Assert.Same(stats, response.Value);
    }
}
