using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Orquestador;

/// <summary>
/// Coordina la ejecución secuencial de todos los agentes del sistema:
/// precio → señales técnicas → noticias → sentimiento → decisión → notificación.
/// Es el punto de entrada central para iniciar un ciclo de análisis.
/// </summary>
public sealed class Orquestador(
    IAgentePrecios agentePrecios,
    IAgenteSeñalesTecnicas agenteSeñales,
    IAgenteNoticias agenteNoticias,
    IAgenteDecision agenteDecision,
    IAgenteNotificacion agenteNotificacion,
    IRepositorioRecomendaciones repositorio,
    IHttpClientFactory fabricaHttp,
    IConfiguration configuracion,
    ILogger<Orquestador> logger)
{
    /// <summary>
    /// Ejecuta el ciclo completo de análisis para un ticker:
    /// obtiene precios, calcula señales técnicas, obtiene noticias,
    /// analiza sentimiento, genera recomendación y notifica si corresponde.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil a analizar (ej. "PLTR").</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La recomendación generada para el ticker.</returns>
    public async Task<Recomendacion> AnalizarAsync(string ticker, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Iniciando análisis para {Ticker}", ticker);

        // 1. Obtener precios históricos
        var cotizaciones = await agentePrecios.ObtenerPreciosAsync(ticker, cancellationToken: cancellationToken);

        // 2. Calcular señales técnicas (RSI, MACD)
        var señales = agenteSeñales.CalcularSeñales(cotizaciones);

        // 3. Obtener titulares de noticias
        var titulares = await agenteNoticias.ObtenerTitularesAsync(ticker, cancellationToken);

        // 4. Analizar sentimiento (microservicio Python — con fallback a neutro si no responde)
        var resultadoSentimiento = await ObtenerSentimientoAsync(titulares, cancellationToken);

        // 5. Generar recomendación
        var recomendacion = agenteDecision.Evaluar(
            ticker,
            señales,
            resultadoSentimiento.Puntaje,
            resultadoSentimiento.TotalAnalizados);

        // 6. Persistir resultado
        await repositorio.GuardarAsync(recomendacion, cancellationToken);

        // 7. Notificar vía Telegram solo si la confianza es ALTA
        if (recomendacion.Confianza == "ALTA")
            await agenteNotificacion.NotificarAsync(recomendacion, cancellationToken);

        logger.LogInformation(
            "Análisis completado para {Ticker}: {Accion} (Confianza: {Confianza})",
            ticker, recomendacion.Accion, recomendacion.Confianza);

        return recomendacion;
    }

    /// <summary>
    /// Llama al microservicio Python de análisis de sentimiento.
    /// Si el servicio no está disponible, retorna sentimiento neutro (0.0)
    /// para no interrumpir el ciclo de análisis.
    /// </summary>
    /// <param name="titulares">Lista de titulares a analizar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado del análisis de sentimiento.</returns>
    private async Task<ResultadoSentimiento> ObtenerSentimientoAsync(
        IReadOnlyList<string> titulares,
        CancellationToken cancellationToken)
    {
        if (titulares.Count == 0)
            return new ResultadoSentimiento(0.0, 0);

        var urlNlp = configuracion["NlpService:Url"] ?? "http://localhost:8000";

        try
        {
            var cliente = fabricaHttp.CreateClient("NlpService");
            var respuesta = await cliente.PostAsJsonAsync(
                $"{urlNlp}/sentiment",
                new { titulares },
                cancellationToken);

            respuesta.EnsureSuccessStatusCode();

            var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoSentimiento>(cancellationToken);
            return resultado ?? new ResultadoSentimiento(0.0, 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Microservicio NLP no disponible. Se usa sentimiento neutro (0.0) para continuar el análisis.");
            return new ResultadoSentimiento(0.0, titulares.Count);
        }
    }
}
