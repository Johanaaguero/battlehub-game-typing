namespace TypingBattle.Api.Matchmaking;

/// <summary>
/// Integración con el Matchmaking Service del Equipo 2: aviso de fin de partida (ADR-004 de battlehub-contracts).
/// Los secretos (ClientSecret) se configuran por variable de entorno o user-secrets, nunca en el repositorio.
/// </summary>
public sealed class MatchmakingOptions
{
    public const string SectionName = "Matchmaking";

    /// <summary>URL base del Matchmaking Service. Vacía: no se avisa el fin de partida (solo queda en el log).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Ruta del callback de ADR-004. <c>{matchId}</c> se reemplaza por el id codificado.</summary>
    public string FinishPath { get; set; } = "/api/matches/{matchId}/finish";

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Intentos totales ante errores temporales (red, 408, 429, 5xx).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Espera antes del primer reintento; se duplica en cada intento.</summary>
    public int RetryDelayMilliseconds { get; set; } = 500;

    /// <summary>Credenciales M2M de Auth0. Sin ellas el aviso viaja sin token (útil contra un Matchmaking local sin auth).</summary>
    public Auth0MachineOptions Auth0 { get; set; } = new();
}

/// <summary>Cliente M2M (client credentials) de Typing Battle en el tenant de Auth0 que administra el Equipo 1.</summary>
public sealed class Auth0MachineOptions
{
    public string Domain { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    /// <summary>Audience de la API de Matchmaking; el token debe traer el permiso <c>matches.finish</c>.</summary>
    public string Audience { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Domain)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(Audience);
}
