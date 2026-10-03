using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Matchmaking;

/// <summary>Cliente HTTP del Matchmaking Service (Equipo 2).</summary>
public sealed class MatchmakingClient(
    IHttpClientFactory httpClientFactory,
    IMachineTokenProvider tokens,
    IOptions<MatchmakingOptions> options,
    ILogger<MatchmakingClient> logger)
{
    public const string HttpClientName = "Matchmaking";

    /// <summary>
    /// Avisa que la partida terminó: <c>POST {BaseUrl}/api/matches/{matchId}/finish</c>, sin cuerpo, porque los resultados
    /// son solo de Typing Battle (ADR-004). El endpoint es idempotente, así que reintentar es seguro.
    /// Nunca lanza por errores de red o de Matchmaking: el resultado ya está guardado. Devuelve true si Matchmaking confirmó.
    /// </summary>
    public async Task<bool> NotifyFinishedAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            logger.LogInformation(
                "Matchmaking no está configurado (Matchmaking:BaseUrl): no se avisa el fin de la partida {MatchId}.", matchId);
            return false;
        }

        var uri = BuildFinishUri(settings, matchId);
        var attempts = Math.Max(1, settings.MaxAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri);
                var token = await tokens.GetTokenAsync(cancellationToken);
                if (token is not null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Matchmaking confirmó el fin de la partida {MatchId}.", matchId);
                    return true;
                }

                if (!IsTransient(response.StatusCode))
                {
                    // 401/403: credenciales M2M; 404: la sala no existe; 409: la sala ya no está en Started (ADR-004).
                    logger.LogWarning(
                        "Matchmaking rechazó el fin de la partida {MatchId}: {StatusCode}.", matchId, (int)response.StatusCode);
                    return false;
                }

                logger.LogWarning(
                    "Matchmaking respondió {StatusCode} al fin de la partida {MatchId} (intento {Attempt} de {Attempts}).",
                    (int)response.StatusCode, matchId, attempt, attempts);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex, "No se pudo avisar a Matchmaking el fin de la partida {MatchId} (intento {Attempt} de {Attempts}).",
                    matchId, attempt, attempts);
            }

            if (attempt < attempts)
            {
                var delay = Math.Max(0, settings.RetryDelayMilliseconds) * (1 << (attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
        }

        logger.LogError(
            "Matchmaking no confirmó el fin de la partida {MatchId} después de {Attempts} intentos.", matchId, attempts);
        return false;
    }

    public static Uri BuildFinishUri(MatchmakingOptions settings, string matchId)
    {
        var path = settings.FinishPath.Replace("{matchId}", Uri.EscapeDataString(matchId), StringComparison.Ordinal);
        return new Uri(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;
}
