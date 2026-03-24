using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockAnalyzer.Api.Datos;

/// <summary>
/// Fábrica de design-time para <see cref="ContextoBd"/> usada por las herramientas
/// de EF Core (dotnet ef migrations). Usa SQLite en tiempo de diseño.
/// </summary>
public sealed class ContextoBdFactory : IDesignTimeDbContextFactory<ContextoBd>
{
    /// <inheritdoc/>
    public ContextoBd CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ContextoBd>()
            .UseSqlite("Data Source=stock_analyzer_design.db")
            .Options;

        return new ContextoBd(opciones);
    }
}
