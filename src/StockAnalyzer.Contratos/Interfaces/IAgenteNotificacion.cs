using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Contratos.Interfaces;

/// <summary>
/// Contrato del agente responsable de enviar notificaciones al usuario
/// a través de Telegram cuando se detecta una señal de alta confianza.
/// </summary>
public interface IAgenteNotificacion
{
    /// <summary>
    /// Envía una notificación al canal de Telegram configurado con la recomendación generada.
    /// Solo debe invocarse cuando la recomendación tenga confianza ALTA.
    /// </summary>
    /// <param name="recomendacion">Recomendación a notificar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task NotificarAsync(Recomendacion recomendacion, CancellationToken cancellationToken = default);
}
