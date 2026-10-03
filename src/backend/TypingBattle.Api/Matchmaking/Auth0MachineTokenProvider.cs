using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Matchmaking;

/// <summary>Obtiene el token de servicio con el que Typing Battle llama a otras APIs.</summary>
public interface IMachineTokenProvider
{
    /// <summary>Devuelve el token vigente, o <c>null</c> si no hay credenciales M2M configuradas.</summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Token M2M de Auth0 por client credentials (ADR-004: el Game Service se autentica ante Matchmaking con OAuth2 M2M).
/// El token se reutiliza hasta un minuto antes de vencer.
/// </summary>
public sealed class Auth0MachineTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MatchmakingOptions> options,
    TimeProvider clock) : IMachineTokenProvider
{
    public const string HttpClientName = "Auth0";

    private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTimeOffset expiresAt;

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var auth0 = options.Value.Auth0;
        if (!auth0.IsConfigured)
        {
            return null;
        }

        if (token is not null && clock.GetUtcNow() < expiresAt)
        {
            return token;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (token is not null && clock.GetUtcNow() < expiresAt)
            {
                return token;
            }

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(
                BuildTokenEndpoint(auth0.Domain),
                new
                {
                    grant_type = "client_credentials",
                    client_id = auth0.ClientId,
                    client_secret = auth0.ClientSecret,
                    audience = auth0.Audience,
                },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(body?.AccessToken))
            {
                throw new HttpRequestException("Auth0 respondió sin access_token.");
            }

            token = body.AccessToken;
            expiresAt = clock.GetUtcNow().AddSeconds(body.ExpiresIn) - RenewalMargin;
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Acepta el dominio con o sin esquema: <c>mi-tenant.us.auth0.com</c> → <c>https://mi-tenant.us.auth0.com/oauth/token</c>.</summary>
    public static Uri BuildTokenEndpoint(string domain)
    {
        var value = domain.Trim().TrimEnd('/');
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = $"https://{value}";
        }

        return new Uri($"{value}/oauth/token");
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
