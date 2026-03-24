using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Contratos.Interfaces;

/// <summary>
/// Contrato del repositorio para persistir y consultar el historial
/// de recomendaciones generadas por el sistema.
/// </summary>
public interface IRepositorioRecomendaciones
{
    /// <summary>
    /// Guarda una recomendación en la base de datos.
    /// </summary>
    /// <param name="recomendacion">Recomendación a persistir.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task GuardarAsync(Recomendacion recomendacion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la última recomendación registrada para un ticker específico.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil del activo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La recomendación más reciente, o null si no existe ninguna.</returns>
    Task<Recomendacion?> ObtenerUltimaAsync(string ticker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el historial de recomendaciones con filtros opcionales por ticker y tipo de acción.
    /// </summary>
    /// <param name="ticker">Filtrar por símbolo bursátil. Null para todos los tickers.</param>
    /// <param name="accion">Filtrar por acción: COMPRAR, VENDER o MANTENER. Null para todas.</param>
    /// <param name="cantidad">Cantidad máxima de resultados. Por defecto 50.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Colección de recomendaciones ordenadas por fecha descendente.</returns>
    Task<IEnumerable<Recomendacion>> ObtenerHistorialAsync(
        string? ticker,
        string? accion,
        int cantidad = 50,
        CancellationToken cancellationToken = default);
}
