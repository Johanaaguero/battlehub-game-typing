using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MySql.EntityFrameworkCore.Extensions;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Persistence;

/// <summary>
/// Base de datos propia de Typing Battle: MySQL con Entity Framework Core
/// (ADR-005 de battlehub-contracts). El esquema evoluciona solo mediante migraciones (carpeta Migrations).
/// </summary>
public sealed class TypingDbContext(DbContextOptions<TypingDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Intercalación binaria para los ids: los de Auth0 distinguen mayúsculas y minúsculas,
    /// y la intercalación por defecto de MySQL 8 (<c>utf8mb4_0900_ai_ci</c>) no.
    /// </summary>
    public const string BinaryCollation = "utf8mb4_bin";

    public DbSet<GameResultEntity> GameResults => Set<GameResultEntity>();
    public DbSet<PlayerResultEntity> PlayerResults => Set<PlayerResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MySQL no guarda la zona horaria: se guarda siempre en UTC y al leer se marca como UTC,
        // para que la API responda con sufijo Z (contrato: todas las fechas en UTC).
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        modelBuilder.Entity<GameResultEntity>(entity =>
        {
            entity.ToTable("game_results");
            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.MatchId).HasMaxLength(ResultValidator.MaxIdLength).ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation(BinaryCollation);
            entity.Property(e => e.GameType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.StartedAt).HasConversion(utcConverter);
            entity.Property(e => e.FinishedAt).HasConversion(utcConverter);
            entity.Property(e => e.CreatedAt).HasConversion(utcConverter);
            entity.Property(e => e.WinnerUserId).HasMaxLength(ResultValidator.MaxIdLength).ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation(BinaryCollation);
            entity.Property(e => e.MetadataJson).HasColumnType("json").IsRequired();
            entity.HasIndex(e => e.FinishedAt);
            entity.HasMany(e => e.Players)
                .WithOne()
                .HasForeignKey(p => p.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerResultEntity>(entity =>
        {
            entity.ToTable("player_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchId).HasMaxLength(ResultValidator.MaxIdLength).ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation(BinaryCollation);
            entity.Property(e => e.UserId).HasMaxLength(ResultValidator.MaxIdLength).ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation(BinaryCollation).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(ResultValidator.MaxIdLength).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.MatchId, e.UserId }).IsUnique();
        });
    }
}
