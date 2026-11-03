using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Hubs;

/// <summary>
/// El hub se prueba sin servidor ni conexiones: los clientes de SignalR (quien llama y el grupo de la partida), el
/// administrador de grupos, el contexto de la conexión, el servicio de resultados y el reloj son dobles de Moq.
/// Lo que se comprueba es qué mensajes recibe cada cliente y qué se le pide al servicio.
/// </summary>
public abstract class TypingHubTestBase
{
    protected const string MatchId = "match-1";
    protected static readonly DateTimeOffset Start = new(2026, 10, 3, 20, 0, 0, TimeSpan.Zero);

    protected readonly Mock<IResultsService> Results = new();
    protected readonly Mock<ISingleClientProxy> Caller = new();
    protected readonly Mock<IClientProxy> Group = new();
    protected readonly Mock<IGroupManager> Groups = new();
    protected readonly Mock<HubCallerContext> Context = new();
    protected readonly TypingMatchService Matches;
    protected readonly TypingHub Hub;

    protected TypingHubTestBase()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(Start);
        Matches = new TypingMatchService(new TypingMatchStore(), Results.Object, clock.Object);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(Caller.Object);
        clients.Setup(c => c.Group(MatchId)).Returns(Group.Object);

        Context.SetupGet(c => c.ConnectionId).Returns("conn-1");

        Hub = new TypingHub(Matches)
        {
            Clients = clients.Object,
            Groups = Groups.Object,
            Context = Context.Object,
        };
    }

    protected sealed record UnexpectedOutcome : SaveResultOutcome;

    /// <summary>Una propiedad de un objeto anónimo, como los que el hub envía en los avisos de falla.</summary>
    protected static object? Property(object? value, string name) => value?.GetType().GetProperty(name)?.GetValue(value);

    protected static GameResultDto CreatedResult() => new(
        MatchId,
        ResultValidator.GameType,
        [new PlayerResultDto("ana", "Ana", 600)],
        Start.UtcDateTime,
        Start.UtcDateTime.AddSeconds(60),
        "ana",
        JsonDocument.Parse("{}").RootElement);

    protected static EndMatchRequest EndRequest(string user = "ana") => new(MatchId, user, null, null, null, null, null);

    // Moq arma las condiciones con árboles de expresión, que no admiten «is»: la coincidencia de patrones va en estos métodos.
    protected static MatchEndedNotification? Ended(object?[] args) => args.Length == 1 ? args[0] as MatchEndedNotification : null;

    protected static bool IsEnded(object?[] args, string outcome) => Ended(args)?.Outcome == outcome;

    protected static string? EndedMatchId(object?[] args) => Ended(args)?.MatchId;

    protected static object? EndedPayload(object?[] args) => Ended(args)?.ResultOrErrors;

    protected static bool EndedHasError(object?[] args, string field) =>
        EndedPayload(args) is IDictionary<string, string[]> errors && errors.ContainsKey(field);

    protected void ResultsReturn(SaveResultOutcome outcome) =>
        Results
            .Setup(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

    protected void VerifyCallerFailure(string method, string reason) =>
        Caller.Verify(
            c => c.SendCoreAsync(
                method,
                It.Is<object?[]>(args => args.Length == 1
                    && Equals(Property(args[0], "reason"), reason)
                    && Equals(Property(args[0], "MatchId"), MatchId)),
                It.IsAny<CancellationToken>()),
            Times.Once);

    protected void VerifyCallerReceived(string method, object notification) =>
        Caller.Verify(
            c => c.SendCoreAsync(method, It.Is<object?[]>(args => args.Length == 1 && Equals(args[0], notification)), It.IsAny<CancellationToken>()),
            Times.Once);

    protected void VerifyCallerReceivedNothing() =>
        Caller.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);

    protected void VerifyGroupReceived(string method, object notification) =>
        Group.Verify(
            g => g.SendCoreAsync(method, It.Is<object?[]>(args => args.Length == 1 && Equals(args[0], notification)), It.IsAny<CancellationToken>()),
            Times.Once);

    protected void VerifyGroupReceivedNothing() =>
        Group.Verify(
            g => g.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);

    protected void VerifyGroupNeverReceived(string method) =>
        Group.Verify(
            g => g.SendCoreAsync(method, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);

    protected async Task JoinAsync(string user, string? name = null) =>
        Assert.True(await Hub.JoinMatch(new JoinMatchRequest(MatchId, user, name)));
}

[Trait("Category", "Unit")]
public class TypingHubTests : TypingHubTestBase
{
    [Fact]
    public void Constructor_Rejects_AMissingService() =>
        Assert.Throws<ArgumentNullException>(() => new TypingHub(null!));

    [Fact]
    public async Task OnDisconnected_DoesNotLeaveTheGroupOnItsOwn()
    {
        await Hub.OnDisconnectedAsync(null);

        Groups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

[Trait("Category", "Unit")]
public class TypingHubJoinMatchTests : TypingHubTestBase
{
    [Fact]
    public async Task AddsTheConnectionToTheGroup_AndAnnouncesThePlayer()
    {
        var joined = await Hub.JoinMatch(new JoinMatchRequest(MatchId, "ana", "Ana"));

        Assert.True(joined);
        Groups.Verify(g => g.AddToGroupAsync("conn-1", MatchId, It.IsAny<CancellationToken>()), Times.Once);
        VerifyGroupReceived("playerJoined", new PlayerJoinedNotification("ana"));
        Assert.True(Matches.IsPlayer(MatchId, "ana"));
    }

    [Fact]
    public async Task TrimsThePlayerId()
    {
        await JoinAsync("  ana  ");

        Assert.True(Matches.IsPlayer(MatchId, "ana"));
        VerifyGroupReceived("playerJoined", new PlayerJoinedNotification("ana"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Fails_WhenThePlayerIsNotInformed(string? currentUser)
    {
        var joined = await Hub.JoinMatch(new JoinMatchRequest(MatchId, currentUser!, "Ana"));

        Assert.False(joined);
        VerifyCallerFailure("joinFailed", "user_required");
        Groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyGroupReceivedNothing();
    }

    [Fact]
    public async Task Fails_WhenTheMatchAlreadyEnded()
    {
        await JoinAsync("ana", "Ana");
        ResultsReturn(new SaveResultOutcome.Created(CreatedResult()));
        await Hub.EndMatch(EndRequest());

        var joined = await Hub.JoinMatch(new JoinMatchRequest(MatchId, "luis", "Luis"));

        Assert.False(joined);
        VerifyCallerFailure("joinFailed", "join_rejected");
        Assert.False(Matches.IsPlayer(MatchId, "luis"));
    }

    [Fact]
    public async Task UsesTheUserId_AsTheNameWhenNoNameIsSent()
    {
        await JoinAsync("ana", "  ");
        ResultsReturn(new SaveResultOutcome.Created(CreatedResult()));

        await Hub.EndMatch(EndRequest());

        Results.Verify(
            r => r.SaveAsync(
                It.Is<SaveResultRequest>(request => request.Players!.Single()!.DisplayName == "ana"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Rejects_ANullRequest() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Hub.JoinMatch(null!));
}

[Trait("Category", "Unit")]
public class TypingHubLeaveMatchTests : TypingHubTestBase
{
    [Fact]
    public async Task RemovesThePlayer_FromTheGroup_AndAnnouncesIt()
    {
        await JoinAsync("ana");

        var left = await Hub.LeaveMatch(new LeaveMatchRequest(MatchId, "ana"));

        Assert.True(left);
        Groups.Verify(g => g.RemoveFromGroupAsync("conn-1", MatchId, It.IsAny<CancellationToken>()), Times.Once);
        VerifyGroupReceived("playerLeft", new PlayerLeftNotification("ana"));
        Assert.False(Matches.IsPlayer(MatchId, "ana"));
    }

    [Fact]
    public async Task Returns_False_AndDoesNotAnnounce_WhenTheMatchDoesNotExist()
    {
        var left = await Hub.LeaveMatch(new LeaveMatchRequest("match-x", "ana"));

        Assert.False(left);
        VerifyGroupReceivedNothing();
    }

    [Fact]
    public async Task Returns_False_WhenThePlayerIsNotInformed()
    {
        var left = await Hub.LeaveMatch(new LeaveMatchRequest(MatchId, " "));

        Assert.False(left);
        Groups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rejects_ANullRequest() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Hub.LeaveMatch(null!));
}

[Trait("Category", "Unit")]
public class TypingHubSendPlayerUpdateTests : TypingHubTestBase
{
    [Fact]
    public async Task BroadcastsTheMetrics_WithTheScoreTheServerCalculates()
    {
        await JoinAsync("ana");

        var sent = await Hub.SendPlayerUpdate(new PlayerUpdateDto(MatchId, "ana", null, 60, 100, Start.UtcDateTime));

        Assert.True(sent);
        // 60 ppm al 100 % = 600 puntos, calculados por el servidor porque el cliente no envió el puntaje.
        VerifyGroupReceived("playerUpdate", new PlayerUpdateNotification("ana", 600, 60, 100));
    }

    [Fact]
    public async Task Fails_WhenThePlayerIsNotInformed()
    {
        var sent = await Hub.SendPlayerUpdate(new PlayerUpdateDto(MatchId, "", null, 60, 100, null));

        Assert.False(sent);
        VerifyCallerFailure("updateFailed", "user_required");
        VerifyGroupReceivedNothing();
    }

    [Fact]
    public async Task Fails_WhenThePlayerDidNotJoinTheMatch()
    {
        var sent = await Hub.SendPlayerUpdate(new PlayerUpdateDto(MatchId, "intruso", null, 60, 100, null));

        Assert.False(sent);
        VerifyCallerFailure("updateFailed", "not_joined");
        VerifyGroupReceivedNothing();
    }

    [Fact]
    public async Task Rejects_ANullUpdate() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Hub.SendPlayerUpdate(null!));
}

[Trait("Category", "Unit")]
public class TypingHubEndMatchTests : TypingHubTestBase
{
    [Fact]
    public async Task Created_IsAnnouncedToTheWholeGroup()
    {
        await JoinAsync("ana", "Ana");
        var created = CreatedResult();
        ResultsReturn(new SaveResultOutcome.Created(created));

        await Hub.EndMatch(EndRequest());

        VerifyGroupReceived("matchEnded", new MatchEndedNotification(MatchId, "created", created));
        VerifyCallerReceivedNothing();
    }

    [Fact]
    public async Task AlreadyExists_IsSentOnlyToWhoAsked_SoTheOthersKeepTheirResult()
    {
        await JoinAsync("ana");
        ResultsReturn(new SaveResultOutcome.AlreadyExists(MatchId));

        await Hub.EndMatch(EndRequest());

        Caller.Verify(
            c => c.SendCoreAsync(
                "matchEnded",
                It.Is<object?[]>(args => IsEnded(args, "already_exists") && EndedMatchId(args) == MatchId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyGroupNeverReceived("matchEnded");
    }

    [Fact]
    public async Task Invalid_IsSentOnlyToWhoAsked_WithTheErrors()
    {
        await JoinAsync("ana");
        var errors = new Dictionary<string, string[]> { ["players"] = ["Debe incluir al menos un jugador."] };
        ResultsReturn(new SaveResultOutcome.Invalid(errors));

        await Hub.EndMatch(EndRequest());

        VerifyCallerReceived("matchEnded", new MatchEndedNotification(MatchId, "invalid", errors));
        VerifyGroupNeverReceived("matchEnded");
    }

    [Fact]
    public async Task AnUnexpectedOutcome_IsReportedAsAnError_ToWhoAsked()
    {
        await JoinAsync("ana");
        ResultsReturn(new UnexpectedOutcome());

        await Hub.EndMatch(EndRequest());

        Caller.Verify(
            c => c.SendCoreAsync(
                "matchEnded",
                It.Is<object?[]>(args => IsEnded(args, "error")
                    && Equals(Property(EndedPayload(args), "message"), "unexpected_outcome")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyGroupNeverReceived("matchEnded");
    }

    [Theory]
    [InlineData("intruso")]
    [InlineData(" ")]
    public async Task OnlyAPlayerOfTheMatchCanEndIt(string user)
    {
        await JoinAsync("ana");

        await Hub.EndMatch(EndRequest(user));

        Caller.Verify(
            c => c.SendCoreAsync(
                "matchEnded",
                It.Is<object?[]>(args => IsEnded(args, "invalid") && EndedHasError(args, "currentUser")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Results.Verify(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyGroupNeverReceived("matchEnded");
    }

    [Fact]
    public async Task PassesTheConnectionAbortToken_ToTheResultsService()
    {
        await JoinAsync("ana");
        using var aborted = new CancellationTokenSource();
        Context.SetupGet(c => c.ConnectionAborted).Returns(aborted.Token);
        ResultsReturn(new SaveResultOutcome.Created(CreatedResult()));

        await Hub.EndMatch(EndRequest());

        Results.Verify(r => r.SaveAsync(It.IsAny<SaveResultRequest>(), aborted.Token), Times.Once);
    }

    [Fact]
    public async Task Rejects_ANullRequest() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Hub.EndMatch(null!));
}
