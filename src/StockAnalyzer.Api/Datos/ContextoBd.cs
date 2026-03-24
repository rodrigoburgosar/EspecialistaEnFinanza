using Microsoft.EntityFrameworkCore;

namespace StockAnalyzer.Api.Datos;

/// <summary>
/// Contexto de base de datos de Entity Framework Core para SQLite.
/// Gestiona la persistencia del historial de recomendaciones.
/// </summary>
public sealed class ContextoBd(DbContextOptions<ContextoBd> opciones) : DbContext(opciones)
{
    /// <summary>Tabla de recomendaciones generadas por el sistema.</summary>
    public DbSet<EntidadRecomendacion> Recomendaciones => Set<EntidadRecomendacion>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntidadRecomendacion>(entidad =>
        {
            entidad.HasKey(r => r.Id);
            entidad.Property(r => r.Id).ValueGeneratedOnAdd();
            entidad.Property(r => r.Ticker).HasMaxLength(10).IsRequired();
            entidad.Property(r => r.Accion).HasMaxLength(10).IsRequired();
            entidad.Property(r => r.Confianza).HasMaxLength(10).IsRequired();
            entidad.HasIndex(r => new { r.Ticker, r.Fecha });
        });
    }
}
