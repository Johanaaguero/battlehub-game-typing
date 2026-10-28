using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.EntityFrameworkCore.Extensions;

namespace TypingBattle.Api.Persistence;

/// <summary>
/// Lo usa <c>dotnet ef</c> para crear migraciones sin arrancar la API. Generar una migración no se
/// conecta a MySQL (por eso basta una cadena sin credenciales); <c>dotnet ef database update</c> sí.
/// La cadena sale siempre de la variable de entorno <c>ConnectionStrings__Typing</c>: no hay una por defecto.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TypingDbContext>
{
    public TypingDbContext CreateDbContext(string[] args)
    {
        var variable = $"ConnectionStrings__{PersistenceServiceCollectionExtensions.ConnectionStringName}";
        var connectionString = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {variable}: dotnet ef la necesita para crear el contexto " +
                "(la misma cadena de conexión que usa la API).");
        }

        var options = new DbContextOptionsBuilder<TypingDbContext>()
            .UseMySQL(connectionString)
            .Options;

        return new TypingDbContext(options);
    }
}
