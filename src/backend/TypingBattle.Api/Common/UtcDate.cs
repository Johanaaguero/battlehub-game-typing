namespace TypingBattle.Api.Common;

/// <summary>
/// Normaliza fechas a UTC. El contrato exige UTC en ISO-8601 con sufijo Z
/// (02-arquitectura-y-flujo.md, «Convención de fechas», de battlehub-contracts).
/// </summary>
public static class UtcDate
{
    /// <summary>
    /// Devuelve la fecha en UTC. Retorna <c>false</c> si la fecha no trae zona horaria:
    /// un texto sin sufijo Z ni offset se deserializa como <see cref="DateTimeKind.Unspecified"/>,
    /// y asumir una zona sería adivinar.
    /// </summary>
    public static bool TryNormalize(DateTime value, out DateTime utc)
    {
        switch (value.Kind)
        {
            case DateTimeKind.Utc:
                utc = value;
                return true;
            case DateTimeKind.Local:
                utc = value.ToUniversalTime();
                return true;
            default:
                utc = default;
                return false;
        }
    }
}
