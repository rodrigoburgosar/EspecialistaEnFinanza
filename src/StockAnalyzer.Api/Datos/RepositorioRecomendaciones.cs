using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Api.Datos;

/// <summary>
/// Repositorio que implementa la persistencia y consulta del historial
/// de recomendaciones usando Entity Framework Core con SQLite.
/// </summary>
public sealed class RepositorioRecomendaciones(ContextoBd contexto) : IRepositorioRecomendaciones
{
    /// <inheritdoc/>
    public async Task GuardarAsync(Recomendacion recomendacion, CancellationToken cancellationToken = default)
    {
        var entidad = new EntidadRecomendacion
        {
            Ticker = recomendacion.Ticker,
            Accion = recomendacion.Accion,
            Confianza = recomendacion.Confianza,
            RSI = recomendacion.RSI,
            Sentimiento = recomendacion.Sentimiento,
            NoticiasAnalizadas = recomendacion.NoticiasAnalizadas,
            Fecha = recomendacion.Fecha
        };

        contexto.Recomendaciones.Add(entidad);
        await contexto.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Recomendacion?> ObtenerUltimaAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var entidad = await contexto.Recomendaciones
            .Where(r => r.Ticker == ticker.ToUpperInvariant())
            .OrderByDescending(r => r.Fecha)
            .FirstOrDefaultAsync(cancellationToken);

        if (entidad is null)
            return null;

        return MapearARecomendacion(entidad);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Recomendacion>> ObtenerHistorialAsync(
        string? ticker,
        string? accion,
        int cantidad = 50,
        CancellationToken cancellationToken = default)
    {
        var consulta = contexto.Recomendaciones.AsQueryable();

        if (!string.IsNullOrWhiteSpace(ticker))
            consulta = consulta.Where(r => r.Ticker == ticker.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(accion))
            consulta = consulta.Where(r => r.Accion == accion.ToUpperInvariant());

        var entidades = await consulta
            .OrderByDescending(r => r.Fecha)
            .Take(cantidad)
            .ToListAsync(cancellationToken);

        return entidades.Select(MapearARecomendacion);
    }

    /// <summary>
    /// Convierte una entidad de base de datos al record de dominio <see cref="Recomendacion"/>.
    /// </summary>
    private static Recomendacion MapearARecomendacion(EntidadRecomendacion entidad) =>
        new(entidad.Ticker, entidad.Accion, entidad.Confianza,
            entidad.RSI, entidad.Sentimiento, entidad.NoticiasAnalizadas, entidad.Fecha);
}
