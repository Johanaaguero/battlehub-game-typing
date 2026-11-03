using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TypingBattle.Api.Persistence;

namespace TypingBattle.UnitTests.Persistence;

/// <summary>
/// El registro de la persistencia se prueba con una configuración en memoria y sin abrir ninguna conexión:
/// el contexto se crea, pero no se consulta. La base de datos real se prueba en integración.
/// </summary>
[Trait("Category", "Unit")]
public class PersistenceServiceCollectionExtensionsTests
{
    private static IConfiguration Configuration(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(connectionString is null
                ? []
                : new Dictionary<string, string?> { ["ConnectionStrings:Typing"] = connectionString })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Throws_AndNamesTheEnvironmentVariable_WhenThereIsNoConnectionString(string? connectionString)
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(
            () => services.AddTypingPersistence(Configuration(connectionString)));

        Assert.Contains("ConnectionStrings__Typing", error.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void RegistersTheDbContext_WithMySql_WithoutConnecting()
    {
        var services = new ServiceCollection();
        services.AddTypingPersistence(Configuration("Server=localhost;Database=typing_unit"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TypingDbContext>();

        Assert.Contains("MySql", db.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistersAHealthCheck_ForTheDatabase()
    {
        var services = new ServiceCollection();
        services.AddTypingPersistence(Configuration("Server=localhost;Database=typing_unit"));

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Assert.Contains(registrations, registration => registration.Name == "mysql");
    }

    [Fact]
    public void ReturnsTheServiceCollection_ToAllowChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddTypingPersistence(Configuration("Server=localhost;Database=typing_unit"));

        Assert.Same(services, returned);
    }
}
