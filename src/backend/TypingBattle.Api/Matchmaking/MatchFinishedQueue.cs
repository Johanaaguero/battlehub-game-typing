using System.Threading.Channels;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Matchmaking;

/// <summary>
/// Implementación de <see cref="IMatchFinishedNotifier"/>: encola el aviso y vuelve de inmediato, para que el hub
/// anuncie el resultado a los jugadores sin esperar a Matchmaking. <see cref="MatchFinishedWorker"/> hace la llamada.
/// </summary>
public sealed class MatchFinishedQueue : IMatchFinishedNotifier
{
    private readonly Channel<string> channel =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<string> Reader => channel.Reader;

    public Task NotifyMatchFinishedAsync(string matchId, SaveResultOutcome outcome, CancellationToken cancellationToken = default)
    {
        // Solo se avisa la partida recién guardada: "already_exists" e "invalid" no cambian el estado de la sala.
        if (outcome is SaveResultOutcome.Created created)
        {
            channel.Writer.TryWrite(created.Result.MatchId);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Toma los avisos de la cola y los envía a Matchmaking, uno por uno.</summary>
public sealed class MatchFinishedWorker(
    MatchFinishedQueue queue,
    MatchmakingClient client,
    ILogger<MatchFinishedWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var matchId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await client.NotifyFinishedAsync(matchId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inesperado al avisar a Matchmaking el fin de la partida {MatchId}.", matchId);
            }
        }
    }
}
