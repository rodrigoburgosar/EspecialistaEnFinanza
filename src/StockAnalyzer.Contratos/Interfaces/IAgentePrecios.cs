using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Contratos.Interfaces;

/// <summary>
/// Contrato del agente responsable de obtener precios históricos OHLCV
/// de un ticker desde proveedores externos (Alpha Vantage, Yahoo Finance).
/// </summary>
public interface IAgentePrecios
{
    /// <summary>
    /// Obtiene los precios históricos del ticker para los últimos N días.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil del activo (ej. "PLTR").</param>
    /// <param name="dias">Cantidad de días hacia atrás a consultar. Por defecto 25 (requerido para Bollinger Bands).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Colección de cotizaciones ordenadas por fecha ascendente.</returns>
    /// <exception cref="InvalidOperationException">Si la API externa no responde o retorna error.</exception>
    Task<IEnumerable<Cotizacion>> ObtenerPreciosAsync(
        string ticker,
        int dias = 25,
        CancellationToken cancellationToken = default);
}
