using Microsoft.AspNetCore.Mvc;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Api.Controllers;

/// <summary>
/// Controlador para pruebas de integración con Telegram.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class NotificacionController(
    IAgenteNotificacion agenteNotificacion,
    ILogger<NotificacionController> logger) : ControllerBase
{
    /// <summary>
    /// Envía una notificación de prueba a Telegram para verificar la integración.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        logger.LogInformation("Enviando notificación de prueba a Telegram...");

        var recomendacionPrueba = new Recomendacion(
            Ticker: "PLTR",
            Accion: "COMPRAR",
            Confianza: "ALTA",
            RSI: 28.5,
            Sentimiento: 0.71,
            NoticiasAnalizadas: 3,
            Fecha: DateTime.UtcNow);

        await agenteNotificacion.NotificarAsync(recomendacionPrueba, cancellationToken);

        return Ok(new { mensaje = "Notificación de prueba enviada. Verificá Telegram." });
    }
}
