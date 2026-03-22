using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Contratos.Interfaces;

/// <summary>
/// Contrato del agente responsable de calcular indicadores técnicos
/// (RSI y MACD) a partir de los precios históricos.
/// </summary>
public interface IAgenteSeñalesTecnicas
{
    /// <summary>
    /// Calcula los indicadores técnicos RSI y MACD a partir de una colección de cotizaciones.
    /// </summary>
    /// <param name="cotizaciones">Colección de precios históricos OHLCV.</param>
    /// <returns>Señales técnicas calculadas con clasificación RSI y confirmación MACD.</returns>
    SeñalesTecnicas CalcularSeñales(IEnumerable<Cotizacion> cotizaciones);
}
