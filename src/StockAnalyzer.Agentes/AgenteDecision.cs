using Microsoft.Extensions.Logging;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Agentes;

/// <summary>
/// Agente responsable de evaluar las señales técnicas y el puntaje de sentimiento
/// para emitir una recomendación de acción (COMPRAR, VENDER o MANTENER)
/// con su nivel de confianza (ALTA o MEDIA).
/// Bollinger Bands actúan como señal de confirmación: elevan la confianza a ALTA
/// cuando el precio confirma el extremo detectado por RSI y sentimiento.
/// </summary>
public sealed class AgenteDecision(ILogger<AgenteDecision> logger) : IAgenteDecision
{
    private const double UmbralSobreventa = 30.0;
    private const double UmbralSobrecompra = 70.0;
    private const double UmbralSentimientoPositivo = 0.3;
    private const double UmbralSentimientoNegativo = -0.3;

    /// <inheritdoc/>
    public Recomendacion Evaluar(
        string ticker,
        SeñalesTecnicas señales,
        double sentimiento,
        int noticiasAnalizadas)
    {
        var (accion, confianza) = DeterminarAccion(señales, sentimiento);

        logger.LogInformation(
            "Decisión para {Ticker}: {Accion} (Confianza: {Confianza}) | RSI={RSI:F1} | Sentimiento={Sentimiento:F2} | BandaInf={BI} | BandaSup={BS}",
            ticker, accion, confianza, señales.RSI, sentimiento, señales.CercaBandaInferior, señales.CercaBandaSuperior);

        return new Recomendacion(
            Ticker: ticker,
            Accion: accion,
            Confianza: confianza,
            RSI: señales.RSI,
            Sentimiento: sentimiento,
            NoticiasAnalizadas: noticiasAnalizadas,
            Fecha: DateTime.UtcNow);
    }

    /// <summary>
    /// Aplica la tabla de decisión combinando RSI, sentimiento y Bollinger Bands.
    /// La confirmación de Bollinger eleva la confianza de MEDIA a ALTA.
    /// </summary>
    /// <param name="señales">Señales técnicas calculadas por <see cref="AgenteSeñalesTecnicas"/>.</param>
    /// <param name="sentimiento">Score de sentimiento entre -1.0 y +1.0.</param>
    /// <returns>Tupla con la acción recomendada y su nivel de confianza.</returns>
    private static (string Accion, string Confianza) DeterminarAccion(SeñalesTecnicas señales, double sentimiento)
    {
        var rsi = señales.RSI;

        // Señal de compra: RSI en sobreventa Y sentimiento positivo
        if (rsi < UmbralSobreventa && sentimiento > UmbralSentimientoPositivo)
        {
            // Bollinger confirma: precio cerca de banda inferior → confianza ALTA
            var confianza = señales.CercaBandaInferior ? "ALTA" : "MEDIA";
            return ("COMPRAR", confianza);
        }

        // Señal de venta: RSI en sobrecompra Y sentimiento negativo
        if (rsi > UmbralSobrecompra && sentimiento < UmbralSentimientoNegativo)
        {
            // Bollinger confirma: precio cerca de banda superior → confianza ALTA
            var confianza = señales.CercaBandaSuperior ? "ALTA" : "MEDIA";
            return ("VENDER", confianza);
        }

        // Señales contradictorias o zona neutral → mantener
        return ("MANTENER", "MEDIA");
    }
}
