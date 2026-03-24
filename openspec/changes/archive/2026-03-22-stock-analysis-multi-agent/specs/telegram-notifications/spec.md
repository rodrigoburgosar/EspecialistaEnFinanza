## ADDED Requirements

### Requirement: Enviar alerta a Telegram ante señal de alta confianza
El sistema SHALL enviar un mensaje formateado al chat de Telegram configurado únicamente cuando el Decision Agent emite una recomendación con confianza "ALTA" (COMPRAR o VENDER).

#### Scenario: Alerta de compra enviada exitosamente
- **WHEN** el Decision Agent emite "COMPRAR" con confianza "ALTA"
- **THEN** el Notification Agent envía mensaje a Telegram con formato:
  `🟢 PLTR — COMPRAR | RSI: 27.3 | Sentimiento: +0.71 | Confianza: ALTA | <timestamp UTC>`

#### Scenario: Alerta de venta enviada exitosamente
- **WHEN** el Decision Agent emite "VENDER" con confianza "ALTA"
- **THEN** el Notification Agent envía mensaje a Telegram con formato:
  `🔴 PLTR — VENDER | RSI: 73.1 | Sentimiento: -0.58 | Confianza: ALTA | <timestamp UTC>`

#### Scenario: Recomendación MANTENER no genera alerta
- **WHEN** el Decision Agent emite "MANTENER" (cualquier confianza)
- **THEN** el Notification Agent NO envía mensaje a Telegram

#### Scenario: Fallo en envío a Telegram
- **WHEN** el bot de Telegram retorna error o el token es inválido
- **THEN** el Notification Agent registra el error en el log pero NO interrumpe el ciclo de análisis ni lanza excepción al Orchestrator

### Requirement: Configuración del bot de Telegram via variables de entorno
El sistema SHALL leer las credenciales del bot Telegram desde variables de entorno, sin valores hardcodeados en el código.

#### Scenario: Variables de entorno configuradas correctamente
- **WHEN** `TELEGRAM_BOT_TOKEN` y `TELEGRAM_CHAT_ID` están definidas en `.env`
- **THEN** el Notification Agent se inicializa correctamente y puede enviar mensajes

#### Scenario: Variables de entorno ausentes
- **WHEN** `TELEGRAM_BOT_TOKEN` o `TELEGRAM_CHAT_ID` no están definidas
- **THEN** el sistema inicia pero el Notification Agent está deshabilitado y registra advertencia en el log al inicio
