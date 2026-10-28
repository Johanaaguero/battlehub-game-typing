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
/// con un usuario que pueda crear y borrar bases). Es obligatoria: no hay un servidor por defecto.
/// </summary>
public sealed class TypingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ServerVariable = "TYPING_TEST_MYSQL";

    public string DatabaseName { get; } = $"typing_test_{Guid.NewGuid():N}";

    private string ConnectionString => $"{ReadServer()};Database={DatabaseName}";

    private static string ReadServer()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {ServerVariable}: la cadena de conexión al servidor MySQL de las pruebas, " +
                "sin Database y con un usuario que pueda crear y borrar bases (consulte el README, en «Pruebas»).");
        }

        return server.TrimEnd(';');
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Typing", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }

    public async Task InitializeAsync()
    {
        _ = ReadServer(); // falla con un mensaje claro si falta la variable, antes de intentar conectar

        try
        {
            // Crear el cliente arranca la API, que aplica las migraciones sobre la base nueva.
            _ = CreateClient();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo preparar MySQL para las pruebas de integración. Verifique que el servidor de " +
                $"{ServerVariable} esté levantado y que el usuario pueda crear y borrar bases.", ex);
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
