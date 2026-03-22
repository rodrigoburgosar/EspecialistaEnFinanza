using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Contratos.Interfaces;

/// <summary>
/// Contrato del agente responsable de evaluar las señales técnicas y de sentimiento
/// para emitir una recomendación de acción sobre el ticker.
/// </summary>
public interface IAgenteDecision
{
    /// <summary>
    /// Evalúa las señales técnicas y el puntaje de sentimiento para generar una recomendación.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil del activo analizado.</param>
    /// <param name="señales">Indicadores técnicos calculados (RSI, MACD).</param>
    /// <param name="sentimiento">Score de sentimiento entre -1.0 y +1.0.</param>
    /// <param name="noticiasAnalizadas">Cantidad de noticias que generaron el score de sentimiento.</param>
    /// <returns>Recomendación con acción (COMPRAR/VENDER/MANTENER) y nivel de confianza.</returns>
    Recomendacion Evaluar(
        string ticker,
        SeñalesTecnicas señales,
        double sentimiento,
        int noticiasAnalizadas);
}
