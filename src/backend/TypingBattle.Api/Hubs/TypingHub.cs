using Microsoft.AspNetCore.SignalR;
using TypingBattle.Api.Auth;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Hubs;

/// <summary>
/// SignalR Hub para Typing Battle. Exponer métodos para join/leave/update/end.
/// Delegar la lógica en TypingMatchService y notificar al grupo usando los contratos de TypingHubContracts.
/// El estado en memoria de la partida se crea con el primer JoinMatch (la sala la administra Matchmaking);
/// no se puede entrar a una partida que ya terminó.
/// Exige un usuario autenticado (política <see cref="TypingPolicies.Play"/>): el jugador es siempre el del token y
/// el currentUser que envía el cliente debe coincidir con él. Solo los jugadores de la partida envían métricas o la terminan.
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

        if (!TryResolveUser(request.CurrentUser, out var userId))
        {
            await Clients.Caller.SendAsync("joinFailed", new { request.MatchId, reason = "user_mismatch" });
            return false;
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? Context.User.GetDisplayName() : request.DisplayName;
        var joined = matchService.TryJoin(request.MatchId, userId, displayName);
        if (!joined)
        {
            // Notificar al cliente que no se pudo unir (faltan datos o la partida ya terminó)
            await Clients.Caller.SendAsync("joinFailed", new { request.MatchId, reason = "join_rejected" });
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, request.MatchId);
        await Clients.Group(request.MatchId).SendAsync("playerJoined", new PlayerJoinedNotification(userId));
        return true;
    }

    public async Task<bool> LeaveMatch(LeaveMatchRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (!TryResolveUser(request.CurrentUser, out var userId))
        {
            return false;
        }

        var left = matchService.TryLeave(request.MatchId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, request.MatchId);
        if (left)
        {
            await Clients.Group(request.MatchId).SendAsync("playerLeft", new PlayerLeftNotification(userId));
        }

        return left;
    }

    public async Task<bool> SendPlayerUpdate(PlayerUpdateDto update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        if (!TryResolveUser(update.CurrentUser, out var userId))
        {
            await Clients.Caller.SendAsync("updateFailed", new { update.MatchId, reason = "user_mismatch" });
            return false;
        }

        if (!matchService.IsPlayer(update.MatchId, userId))
        {
            await Clients.Caller.SendAsync("updateFailed", new { update.MatchId, reason = "not_joined" });
            return false;
        }

        var ok = matchService.TryUpdatePlayerMetrics(
            update.MatchId, userId, update.Score, update.Wpm, update.Accuracy, out var currentScore);
        if (!ok)
        {
            await Clients.Caller.SendAsync("updateFailed", new { update.MatchId, reason = "match_not_found" });
            return false;
        }

        // Se retransmite el puntaje vigente del servidor (lo calcula él si el cliente no lo envió).
        await Clients.Group(update.MatchId).SendAsync("playerUpdate", new PlayerUpdateNotification(userId, currentScore, update.Wpm, update.Accuracy));
        return true;
    }

    public async Task EndMatch(EndMatchRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (!TryResolveUser(request.CurrentUser, out var userId) || !matchService.IsPlayer(request.MatchId, userId))
        {
            var errors = new Dictionary<string, string[]>
            {
                ["currentUser"] = ["Solo un jugador de la partida puede terminarla."],
            };
            await Clients.Caller.SendAsync("matchEnded", new MatchEndedNotification(request.MatchId, "invalid", errors));
            return;
        }

        var outcome = await matchService.EndMatchAsync(request with { CurrentUser = userId }, Context.ConnectionAborted);

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

    /// <summary>
    /// Jugador de la conexión: el claim <c>sub</c> del token. Devuelve false si el cliente dice ser otro usuario.
    /// </summary>
    private bool TryResolveUser(string? claimedUserId, out string userId)
    {
        userId = Context.User.GetUserId() ?? "";
        return userId.Length > 0
            && (string.IsNullOrWhiteSpace(claimedUserId) || string.Equals(claimedUserId.Trim(), userId, StringComparison.Ordinal));
    }
}
