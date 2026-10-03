using Microsoft.AspNetCore.SignalR;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Hubs;

/// <summary>
/// SignalR Hub para Typing Battle. Exponer métodos para join/leave/update/end.
/// Delegar la lógica en TypingMatchService y notificar al grupo usando los contratos de TypingHubContracts.
/// El estado en memoria de la partida se crea con el primer JoinMatch (la sala la administra Matchmaking);
/// no se puede entrar a una partida que ya terminó.
/// </summary>
public sealed class TypingHub : Hub
{
    private readonly TypingMatchService matchService;

    public TypingHub(TypingMatchService matchService)
    {
        this.matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // No hacemos limpieza automática de grupos aquí: LeaveMatch debe ser llamado explícitamente.
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<bool> JoinMatch(JoinMatchRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var joined = matchService.TryJoin(request.MatchId, request.CurrentUser, request.DisplayName);
        if (!joined)
        {
            // Notificar al cliente que no se pudo unir (faltan datos o la partida ya terminó)
            await Clients.Caller.SendAsync("joinFailed", new { request.MatchId, reason = "join_rejected" });
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, request.MatchId);
        await Clients.Group(request.MatchId).SendAsync("playerJoined", new PlayerJoinedNotification(request.CurrentUser));
        return true;
    }

    public async Task<bool> LeaveMatch(LeaveMatchRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var left = matchService.TryLeave(request.MatchId, request.CurrentUser);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, request.MatchId);
        if (left)
        {
            await Clients.Group(request.MatchId).SendAsync("playerLeft", new PlayerLeftNotification(request.CurrentUser));
        }

        return left;
    }

    public async Task<bool> SendPlayerUpdate(PlayerUpdateDto update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        var ok = matchService.TryUpdatePlayerMetrics(
            update.MatchId, update.CurrentUser, update.Score, update.Wpm, update.Accuracy, out var currentScore);
        if (!ok)
        {
            await Clients.Caller.SendAsync("updateFailed", new { update.MatchId, reason = "match_not_found" });
            return false;
        }

        // Se retransmite el puntaje vigente del servidor (lo calcula él si el cliente no lo envió).
        await Clients.Group(update.MatchId).SendAsync("playerUpdate", new PlayerUpdateNotification(update.CurrentUser, currentScore, update.Wpm, update.Accuracy));
        return true;
    }

    public async Task EndMatch(EndMatchRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var outcome = await matchService.EndMatchAsync(request, Context.ConnectionAborted);

        switch (outcome)
        {
            case SaveResultOutcome.Created created:
                await Clients.Group(request.MatchId).SendAsync("matchEnded", new MatchEndedNotification(request.MatchId, "created", created.Result));
                break;
            case SaveResultOutcome.AlreadyExists exists:
                // Cada cliente pide terminar al acabarse su tiempo: el primero guarda y avisa a todos con "created".
                // Los demás solo reciben "already_exists" ellos mismos, para no pisar el resultado ya mostrado.
                await Clients.Caller.SendAsync("matchEnded", new MatchEndedNotification(request.MatchId, "already_exists", new { matchId = exists.MatchId }));
                break;
            case SaveResultOutcome.Invalid invalid:
                // Send errors to the caller (validation failed)
                await Clients.Caller.SendAsync("matchEnded", new MatchEndedNotification(request.MatchId, "invalid", invalid.Errors));
                break;
            default:
                // Unexpected outcome: notify caller
                await Clients.Caller.SendAsync("matchEnded", new MatchEndedNotification(request.MatchId, "error", new { message = "unexpected_outcome" }));
                break;
        }
    }
}
