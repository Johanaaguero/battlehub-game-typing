using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.EntityFrameworkCore.Extensions;

namespace TypingBattle.Api.Persistence;

/// <summary>
/// Lo usa <c>dotnet ef</c> para crear migraciones sin arrancar la API. Generar una migración no se
/// conecta a MySQL; <c>dotnet ef database update</c> sí, y toma la cadena de la variable de entorno
/// <c>ConnectionStrings__Typing</c> si existe.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TypingDbContext>
{
    public TypingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Typing")
            ?? PersistenceServiceCollectionExtensions.LocalDevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<TypingDbContext>()
            .UseMySQL(connectionString)
            .Options;

        return new TypingDbContext(options);
    }
}
