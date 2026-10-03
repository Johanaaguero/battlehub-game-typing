using System.Security.Claims;

namespace TypingBattle.Api.Auth;

/// <summary>Identidad del usuario a partir de los claims del token (sin transformar los nombres: <c>sub</c> sigue siendo <c>sub</c>).</summary>
public static class UserIdentity
{
    /// <summary>Id del usuario: el claim <c>sub</c> (en Auth0 tiene la forma <c>auth0|64f0c1...</c>).</summary>
    public static string? GetUserId(this ClaimsPrincipal? user) =>
        Clean(user?.FindFirst("sub")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value);

    /// <summary>
    /// Nombre para mostrar. Los access tokens de Auth0 suelen traer solo <c>sub</c>; por eso el cliente también
    /// envía el nombre al unirse y esto queda como respaldo.
    /// </summary>
    public static string GetDisplayName(this ClaimsPrincipal? user) =>
        Clean(user?.FindFirst("name")?.Value)
        ?? Clean(user?.FindFirst("nickname")?.Value)
        ?? user.GetUserId()
        ?? "Jugador";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
