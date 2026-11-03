using TypingBattle.Api.Persistence;

namespace TypingBattle.UnitTests.Persistence;

/// <summary>
/// La fábrica que usa <c>dotnet ef</c> lee la cadena de conexión de una variable de entorno y no trae ninguna por defecto.
/// Crear el contexto no abre conexión. La variable se restaura al terminar cada prueba.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DesignTimeDbContextFactoryTests : IDisposable
{
    private const string Variable = "ConnectionStrings__Typing";

    private readonly string? original = Environment.GetEnvironmentVariable(Variable);

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, original);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Throws_AndNamesTheVariable_WhenThereIsNoConnectionString(string? value)
    {
        Environment.SetEnvironmentVariable(Variable, value);

        var error = Assert.Throws<InvalidOperationException>(() => new DesignTimeDbContextFactory().CreateDbContext([]));

        Assert.Contains(Variable, error.Message);
    }

    [Fact]
    public void CreatesTheContext_WithMySql_WithoutConnecting()
    {
        Environment.SetEnvironmentVariable(Variable, "Server=localhost;Database=typing_design");

        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Contains("MySql", db.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
