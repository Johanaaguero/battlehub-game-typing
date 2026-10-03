using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TypingBattle.Api.Persistence;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Levanta la API completa contra un MySQL real (04-persistencia-y-api-juegos.md exige probar contra
/// una base real). Cada instancia usa una base de datos propia y desechable (<c>typing_test_…</c>),
/// creada con las migraciones al arrancar y eliminada al terminar.
///
/// El servidor se toma de la variable de entorno <c>TYPING_TEST_MYSQL</c> (cadena sin <c>Database</c>,
/// con un usuario que pueda crear y borrar bases). Sin ella se usa el MySQL de docker-compose.yml.
/// </summary>
public sealed class TypingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ServerVariable = "TYPING_TEST_MYSQL";

    private const string DefaultServer = "Server=localhost;Port=3306;User ID=root;Password=typing_root";

    public string DatabaseName { get; } = $"typing_test_{Guid.NewGuid():N}";

    private string ConnectionString
    {
        get
        {
            var server = Environment.GetEnvironmentVariable(ServerVariable);
            server = string.IsNullOrWhiteSpace(server) ? DefaultServer : server.TrimEnd(';');
            return $"{server};Database={DatabaseName}";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Typing", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        // Identidad de desarrollo (X-Dev-User / dev_user): las pruebas no dependen de un tenant de Auth0.
        builder.UseSetting("Auth:Mode", "Development");
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Crear el cliente arranca la API, que aplica las migraciones sobre la base nueva.
            _ = CreateClient();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo preparar MySQL para las pruebas de integración. Levante el MySQL local con " +
                $"'docker compose up -d mysql' o configure la variable de entorno {ServerVariable}.", ex);
        }

        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TypingDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
