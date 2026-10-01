using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;

namespace TypingBattle.Api.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Typing";

    /// <summary>
    /// Cadena del MySQL local de docker-compose.yml. Solo sirve en desarrollo: en cualquier otro entorno
    /// se configura <c>ConnectionStrings__Typing</c> por variable de entorno.
    /// </summary>
    public const string LocalDevelopmentConnectionString =
        "Server=localhost;Port=3306;Database=typing_battle;User ID=typing;Password=typing_dev";

    /// <summary>Registra el <see cref="TypingDbContext"/> contra MySQL (ADR-005) y su comprobación de salud.</summary>
    public static IServiceCollection AddTypingPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}' (variable de entorno ConnectionStrings__{ConnectionStringName}).");
        }

        services.AddDbContext<TypingDbContext>(options => options.UseMySQL(connectionString));
        services.AddHealthChecks().AddDbContextCheck<TypingDbContext>("mysql");
        return services;
    }

    /// <summary>
    /// Aplica las migraciones pendientes al arrancar si <c>Database:MigrateOnStartup</c> es <c>true</c>
    /// (activado en Development). En otros entornos se aplican con <c>dotnet ef database update</c>.
    /// </summary>
    public static async Task MigrateTypingDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TypingDbContext>();
        await db.Database.MigrateAsync();
    }
}
