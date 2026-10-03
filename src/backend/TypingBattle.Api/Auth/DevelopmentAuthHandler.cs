using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Auth;

/// <summary>
/// Autenticación SOLO para desarrollo local y pruebas: confía en el usuario que declare el cliente, sin token.
/// Permite jugar con el Shell simulado del Equipo 3, que todavía no emite tokens de Auth0. Nunca llega a producción:
/// <see cref="AuthServiceCollectionExtensions.AddTypingAuth"/> se niega a registrarla fuera de Development y Testing.
/// </summary>
/// <remarks>
/// Identidad por encabezado (<c>X-Dev-User</c>, <c>X-Dev-Name</c>, <c>X-Dev-Permissions</c>) o, para el hub (un
/// WebSocket del navegador no puede mandar encabezados), por la URL (<c>dev_user</c>, <c>dev_name</c>, <c>dev_permissions</c>).
/// </remarks>
public sealed class DevelopmentAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = FromHeader("X-Dev-User") ?? Request.Query["dev_user"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        userId = userId.Trim();
        var name = FromHeader("X-Dev-Name") ?? Request.Query["dev_name"].FirstOrDefault();
        var claims = new List<Claim>
        {
            new("sub", userId),
            new("name", string.IsNullOrWhiteSpace(name) ? userId : name.Trim()),
        };

        // Permisos simulados para probar las políticas sin Auth0, separados por coma o espacio.
        var permissions = Request.Headers["X-Dev-Permissions"].FirstOrDefault() ?? Request.Query["dev_permissions"].FirstOrDefault();
        foreach (var permission in (permissions ?? "").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim("permissions", permission));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, nameType: "name", roleType: "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    /// <summary>
    /// Los encabezados HTTP no admiten tildes ni eñes: el microfrontend los envía codificados (encodeURIComponent).
    /// Los parámetros de la URL ya llegan decodificados.
    /// </summary>
    private string? FromHeader(string name)
    {
        var value = Request.Headers[name].FirstOrDefault();
        return value is null ? null : Uri.UnescapeDataString(value);
    }
}
