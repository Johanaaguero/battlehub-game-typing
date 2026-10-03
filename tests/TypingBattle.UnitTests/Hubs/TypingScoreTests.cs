using TypingBattle.Api.Hubs;

namespace TypingBattle.UnitTests.Hubs;

[Trait("Category", "Unit")]
public class TypingScoreTests
{
    [Theory]
    [InlineData(60.0, 100.0, 600)]
    [InlineData(62.4, 96.1, 600)]
    [InlineData(40.0, 90.0, 360)]
    [InlineData(0.0, 100.0, 0)]
    [InlineData(80.0, 0.0, 0)]
    public void Calculate_MultiplicaVelocidadPorPrecisionPorDiez(double wpm, double accuracy, int expected)
    {
        Assert.Equal(expected, TypingScore.Calculate(wpm, accuracy));
    }

    [Theory]
    [InlineData(-10.0, 90.0, 0)]
    [InlineData(50.0, 150.0, 500)]
    [InlineData(50.0, -5.0, 0)]
    [InlineData(double.NaN, 90.0, 0)]
    [InlineData(double.PositiveInfinity, 90.0, 0)]
    public void Calculate_AcotaValoresFueraDeRango(double wpm, double accuracy, int expected)
    {
        Assert.Equal(expected, TypingScore.Calculate(wpm, accuracy));
    }

    [Fact]
    public void Calculate_NoDesbordaConVelocidadesEnormes()
    {
        Assert.Equal(int.MaxValue, TypingScore.Calculate(1e12, 100.0));
    }
}
