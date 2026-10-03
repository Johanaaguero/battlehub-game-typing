using System.Security.Claims;

namespace TypingBattle.Api.Auth;

public static class TypingPolicies
{
    /// <summary>
    /// Jugar y consultar resultados: la exigen el hub y los GET de la API. Siempre pide un usuario autenticado y,
    /// si se configura <see cref="AuthOptions.RequiredPermission"/>, también ese permiso.
    /// </summary>
    public const string Play = "TypingPlay";

    /// <summary>Registrar resultados por HTTP: exige <see cref="AuthOptions.ResultsWritePermission"/>.</summary>
    public const string ResultsWriter = "TypingResultsWriter";
}

/// <summary>Lee los permisos que trae el token.</summary>
public static class PermissionCheck
{
    /// <summary>
    /// Indica si el usuario tiene el permiso. Acepta el claim <c>permissions</c> (Auth0 con RBAC) y el claim
    /// <c>scope</c> (lista separada por espacios, típico de los tokens M2M). La comparación es exacta y distingue
    /// mayúsculas: <c>games.typing.play.admin</c> no cuenta como <c>games.typing.play</c>.
    /// </summary>
    public static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        foreach (var claim in user.Claims)
        {
            if (claim.Type == "permissions" && string.Equals(claim.Value, permission, StringComparison.Ordinal))
            {
                return true;
            }

            if (claim.Type == "scope"
                && claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
