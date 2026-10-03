namespace TypingBattle.Api.Auth;

/// <summary>Configuración de autenticación. Sección de configuración: <c>Auth</c>.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string Auth0Mode = "Auth0";
    public const string DevelopmentMode = "Development";

    /// <summary>
    /// <c>Auth0</c> valida los JWT del tenant compartido del proyecto (lo administra el Equipo 1).
    /// <c>Development</c> acepta el usuario que indique el cliente, sin token, y solo se permite en los entornos
    /// Development y Testing: sirve para trabajar con el Shell simulado, que todavía no emite tokens.
    /// </summary>
    public string Mode { get; set; } = Auth0Mode;

    /// <summary>Dominio del tenant de Auth0, por ejemplo <c>mi-tenant.us.auth0.com</c>.</summary>
    public string Domain { get; set; } = "";

    /// <summary>Identificador (audience) de la API que deben traer los tokens.</summary>
    public string Audience { get; set; } = "";

    /// <summary>
    /// Permiso que debe traer el token para jugar y consultar resultados, por ejemplo <c>games.typing.play</c>
    /// (03-contratos-tecnicos.md). Vacío: basta con un usuario autenticado. Requiere que Auth0 emita los permisos
    /// en el token (RBAC con «Add Permissions in the Access Token»).
    /// </summary>
    public string RequiredPermission { get; set; } = "";

    /// <summary>
    /// Permiso para <c>POST /api/games/typing/results</c>. Según 04-persistencia-y-api-juegos.md el resultado lo
    /// registra el backend del juego (el hub lo hace sin HTTP), así que este endpoint queda para servicios con un
    /// token M2M que traiga este permiso, nunca para el navegador de un jugador.
    /// </summary>
    public string ResultsWritePermission { get; set; } = "games.typing.results.write";
}
