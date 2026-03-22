using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Agentes;

/// <summary>
/// Agente responsable de calcular los indicadores técnicos RSI, MACD,
/// Bollinger Bands (20 períodos, 2σ) y EMA 20 usando Skender.Stock.Indicators.
/// </summary>
public sealed class AgenteSeñalesTecnicas(ILogger<AgenteSeñalesTecnicas> logger) : IAgenteSeñalesTecnicas
{
    private const int PeriodosRsi = 14;
    private const int PeriodosBollinger = 20;
    private const int PeriodosEma = 20;
    private const double UmbralSobreventa = 30.0;
    private const double UmbralSobrecompra = 70.0;
    private const double MargenBandaPorcentaje = 0.02;

    /// <inheritdoc/>
    public SeñalesTecnicas CalcularSeñales(IEnumerable<Cotizacion> cotizaciones)
    {
        var lista = cotizaciones.ToList();

        if (lista.Count < PeriodosRsi)
        {
            logger.LogWarning(
                "Datos insuficientes para calcular indicadores: se tienen {Cantidad} registros, se requieren {Minimo}. Retornando valores por defecto.",
                lista.Count, PeriodosRsi);
            return new SeñalesTecnicas(
                RSI: 50.0, ClasificacionRSI: "NEUTRAL",
                MACD: 0.0, ConfirmacionAlcista: false,
                BollingerSuperior: 0.0, BollingerInferior: 0.0,
                CercaBandaInferior: false, CercaBandaSuperior: false,
                EMA20: 0.0, TendenciaAlcista: false);
        }

        var quotes = lista.Select(c => new Quote
        {
            Date = c.Fecha.ToDateTime(TimeOnly.MinValue),
            Open = c.Apertura,
            High = c.Maximo,
            Low = c.Minimo,
            Close = c.Cierre,
            Volume = c.Volumen
        }).ToList();

        var rsi = CalcularRsi(quotes);
        var (macd, confirmacionAlcista) = CalcularMacd(quotes);
        var precioActual = (double)lista.Last().Cierre;
        var (bollingerSuperior, bollingerInferior, cercaBandaInferior, cercaBandaSuperior) =
            CalcularBollinger(quotes, precioActual, lista.Count);
        var (ema20, tendenciaAlcista) = CalcularEma(quotes, precioActual, lista.Count);

        logger.LogInformation(
            "Señales calculadas — RSI: {RSI:F2} ({Clasificacion}), MACD: {MACD:F4}, " +
            "BandaInf: {BI:F2}, BandaSup: {BS:F2}, CercaInf: {CI}, CercaSup: {CS}, " +
            "EMA20: {EMA:F2}, TendenciaAlcista: {TA}",
            rsi, ClasificarRsi(rsi), macd,
            bollingerInferior, bollingerSuperior, cercaBandaInferior, cercaBandaSuperior,
            ema20, tendenciaAlcista);

        return new SeñalesTecnicas(
            RSI: rsi,
            ClasificacionRSI: ClasificarRsi(rsi),
            MACD: macd,
            ConfirmacionAlcista: confirmacionAlcista,
            BollingerSuperior: bollingerSuperior,
            BollingerInferior: bollingerInferior,
            CercaBandaInferior: cercaBandaInferior,
            CercaBandaSuperior: cercaBandaSuperior,
            EMA20: ema20,
            TendenciaAlcista: tendenciaAlcista);
    }

    /// <summary>
    /// Calcula el RSI de 14 períodos y retorna el último valor disponible.
    /// </summary>
    private static double CalcularRsi(List<Quote> quotes)
    {
        var resultados = quotes.GetRsi(PeriodosRsi).ToList();
        return resultados.LastOrDefault(r => r.Rsi.HasValue)?.Rsi ?? 50.0;
    }

    /// <summary>
    /// Calcula el MACD y determina si hay confirmación alcista (cruce hacia arriba).
    /// </summary>
    private static (double Macd, bool ConfirmacionAlcista) CalcularMacd(List<Quote> quotes)
    {
        var resultados = quotes.GetMacd().ToList();
        var ultimo = resultados.LastOrDefault(r => r.Macd.HasValue);
        var penultimo = resultados.LastOrDefault(r => r.Macd.HasValue && r != ultimo);

        if (ultimo is null)
            return (0.0, false);

        var valorMacd = ultimo.Macd ?? 0.0;
        var confirmacion = penultimo is not null
            && penultimo.Macd < penultimo.Signal
            && ultimo.Macd >= ultimo.Signal;

        return (valorMacd, confirmacion);
    }

    /// <summary>
    /// Calcula Bollinger Bands de 20 períodos con 2 desviaciones estándar.
    /// Retorna valores por defecto (0, false) si hay menos de 20 cotizaciones.
    /// </summary>
    /// <param name="quotes">Lista de quotes.</param>
    /// <param name="precioActual">Precio de cierre actual para calcular proximidad a las bandas.</param>
    /// <param name="cantidad">Total de cotizaciones disponibles.</param>
    private static (double Superior, double Inferior, bool CercaInferior, bool CercaSuperior) CalcularBollinger(
        List<Quote> quotes, double precioActual, int cantidad)
    {
        if (cantidad < PeriodosBollinger)
            return (0.0, 0.0, false, false);

        var resultados = quotes.GetBollingerBands(PeriodosBollinger, 2).ToList();
        var ultimo = resultados.LastOrDefault(r => r.UpperBand.HasValue && r.LowerBand.HasValue);

        if (ultimo is null)
            return (0.0, 0.0, false, false);

        var superior = ultimo.UpperBand!.Value;
        var inferior = ultimo.LowerBand!.Value;

        var cercaInferior = inferior > 0
            && Math.Abs(precioActual - inferior) / inferior <= MargenBandaPorcentaje;
        var cercaSuperior = superior > 0
            && Math.Abs(precioActual - superior) / superior <= MargenBandaPorcentaje;

        return (superior, inferior, cercaInferior, cercaSuperior);
    }

    /// <summary>
    /// Calcula la EMA de 20 períodos y determina si el precio está por encima (tendencia alcista).
    /// Retorna (0, false) si hay menos de 20 cotizaciones.
    /// </summary>
    /// <param name="quotes">Lista de quotes.</param>
    /// <param name="precioActual">Precio de cierre actual para comparar con la EMA.</param>
    /// <param name="cantidad">Total de cotizaciones disponibles.</param>
    private static (double Ema20, bool TendenciaAlcista) CalcularEma(
        List<Quote> quotes, double precioActual, int cantidad)
    {
        if (cantidad < PeriodosEma)
            return (0.0, false);

        var resultados = quotes.GetEma(PeriodosEma).ToList();
        var ultimoEma = resultados.LastOrDefault(r => r.Ema.HasValue)?.Ema ?? 0.0;

        return (ultimoEma, precioActual > ultimoEma);
    }

    /// <summary>
    /// Clasifica el valor del RSI en SOBREVENTA, NEUTRAL o SOBRECOMPRA.
    /// </summary>
    /// <param name="rsi">Valor del RSI a clasificar.</param>
    private static string ClasificarRsi(double rsi) => rsi switch
    {
        < UmbralSobreventa => "SOBREVENTA",
        > UmbralSobrecompra => "SOBRECOMPRA",
        _ => "NEUTRAL"
    };
}
