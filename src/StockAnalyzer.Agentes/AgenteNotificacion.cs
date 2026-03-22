using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;
using Telegram.Bot;

namespace StockAnalyzer.Agentes;

/// <summary>
/// Agente responsable de enviar alertas al usuario vía Telegram cuando
/// el sistema detecta una señal de alta confianza (COMPRAR o VENDER).
/// Si las credenciales no están configuradas, el agente se deshabilita
/// sin interrumpir el flujo del sistema.
/// </summary>
public sealed class AgenteNotificacion : IAgenteNotificacion
{
    private readonly ILogger<AgenteNotificacion> _logger;
    private readonly ITelegramBotClient? _clienteBot;
    private readonly long _chatId;
    private readonly bool _habilitado;

    /// <summary>
    /// Inicializa el agente de notificación leyendo las credenciales de Telegram
    /// desde la configuración. Si faltan, el agente queda deshabilitado.
    /// </summary>
    public AgenteNotificacion(IConfiguration configuracion, ILogger<AgenteNotificacion> logger)
    {
        _logger = logger;

        var token = configuracion["Telegram:BotToken"];
        var chatIdTexto = configuracion["Telegram:ChatId"];

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatIdTexto))
        {
            _logger.LogWarning(
                "Telegram no configurado (TELEGRAM_BOT_TOKEN o TELEGRAM_CHAT_ID ausentes). Las notificaciones están deshabilitadas.");
            _habilitado = false;
            return;
        }

        if (!long.TryParse(chatIdTexto, out _chatId))
        {
            _logger.LogWarning("TELEGRAM_CHAT_ID tiene un formato inválido. Las notificaciones están deshabilitadas.");
            _habilitado = false;
            return;
        }

        _clienteBot = new TelegramBotClient(token);
        _habilitado = true;
        _logger.LogInformation("Agente de notificación Telegram inicializado correctamente.");
    }

    /// <inheritdoc/>
    public async Task NotificarAsync(Recomendacion recomendacion, CancellationToken cancellationToken = default)
    {
        if (!_habilitado || _clienteBot is null)
            return;

        if (recomendacion.Confianza != "ALTA")
            return;

        var mensaje = FormatearMensaje(recomendacion);

        try
        {
            await _clienteBot.SendMessage(
                chatId: _chatId,
                text: mensaje,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Notificación Telegram enviada para {Ticker}: {Accion}",
                recomendacion.Ticker, recomendacion.Accion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al enviar notificación Telegram para {Ticker}. El análisis continúa.",
                recomendacion.Ticker);
        }
    }

    /// <summary>
    /// Formatea el mensaje de Telegram con emoji, ticker, acción y métricas.
    /// </summary>
    /// <param name="recomendacion">Datos de la recomendación a notificar.</param>
    /// <returns>Texto formateado en Markdown para Telegram.</returns>
    private static string FormatearMensaje(Recomendacion recomendacion)
    {
        var emoji = recomendacion.Accion == "COMPRAR" ? "🟢" : "🔴";
        var sentimientoTexto = recomendacion.Sentimiento >= 0
            ? $"+{recomendacion.Sentimiento:F2}"
            : $"{recomendacion.Sentimiento:F2}";

        return $"{emoji} *{recomendacion.Ticker}* — {recomendacion.Accion}\n" +
               $"RSI: {recomendacion.RSI:F1} | Sentimiento: {sentimientoTexto}\n" +
               $"Confianza: {recomendacion.Confianza} | Noticias: {recomendacion.NoticiasAnalizadas}\n" +
               $"_{recomendacion.Fecha:yyyy-MM-dd HH:mm} UTC_";
    }
}
