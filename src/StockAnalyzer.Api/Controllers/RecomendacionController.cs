using Microsoft.AspNetCore.Mvc;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;
using OrquestadorNS = StockAnalyzer.Orquestador;

namespace StockAnalyzer.Api.Controllers;

/// <summary>
/// Controlador REST que expone los endpoints de análisis y consulta de recomendaciones
/// generadas por el sistema de análisis multi-agente.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class RecomendacionController(
    IRepositorioRecomendaciones repositorio,
    OrquestadorNS.Orquestador orquestador,
    ILogger<RecomendacionController> logger) : ControllerBase
{
    /// <summary>
    /// Obtiene la última recomendación generada para el ticker especificado.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil del activo (ej. "PLTR").</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La recomendación más reciente o HTTP 404 si no existe.</returns>
    [HttpGet("{ticker}")]
    [ProducesResponseType(typeof(Recomendacion), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Recomendacion>> ObtenerUltima(
        string ticker,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Consultando última recomendación para {Ticker}", ticker);

        var recomendacion = await repositorio.ObtenerUltimaAsync(ticker.ToUpperInvariant(), cancellationToken);

        if (recomendacion is null)
        {
            logger.LogWarning("No se encontró recomendación para {Ticker}", ticker);
            return NotFound($"No hay recomendaciones registradas para el ticker '{ticker}'.");
        }

        return Ok(recomendacion);
    }

    /// <summary>
    /// Dispara un ciclo completo de análisis para el ticker especificado y retorna la recomendación generada.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil a analizar (ej. "PLTR").</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La recomendación generada por el sistema multi-agente.</returns>
    [HttpPost("~/api/analisis/{ticker}")]
    [ProducesResponseType(typeof(Recomendacion), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Recomendacion>> Analizar(
        string ticker,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Análisis manual disparado para {Ticker}", ticker);

        var recomendacion = await orquestador.AnalizarAsync(ticker.ToUpperInvariant(), cancellationToken);
        return Ok(recomendacion);
    }
}
