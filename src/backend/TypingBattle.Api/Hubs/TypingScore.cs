namespace TypingBattle.Api.Hubs;

/// <summary>
/// Regla de puntaje de Typing Battle: velocidad neta × precisión × 10 (60 ppm al 100 % = 600 puntos).
/// Es la misma fórmula del repositorio de referencia del equipo. Lógica pura, sin dependencias.
/// </summary>
public static class TypingScore
{
    /// <summary>
    /// Calcula el puntaje entero. Los valores fuera de rango se acotan: la velocidad nunca es negativa
    /// y la precisión queda entre 0 y 100.
    /// </summary>
    public static int Calculate(double wpm, double accuracy)
    {
        var safeWpm = double.IsFinite(wpm) ? Math.Max(0, wpm) : 0;
        var safeAccuracy = double.IsFinite(accuracy) ? Math.Clamp(accuracy, 0, 100) : 0;

        var score = Math.Round(safeWpm * safeAccuracy / 100.0 * 10, MidpointRounding.AwayFromZero);
        return score >= int.MaxValue ? int.MaxValue : (int)score;
    }
}
