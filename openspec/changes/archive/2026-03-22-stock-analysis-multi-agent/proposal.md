## Why

Se necesita un sistema automatizado que combine análisis técnico (RSI) con análisis de sentimiento de noticias para generar recomendaciones de compra/venta de acciones de Palantir (PLTR), eliminando la necesidad de monitoreo manual y permitiendo reaccionar a señales de mercado en tiempo real desde el celular vía Telegram.

## What Changes

- **Nuevo sistema completo** de análisis de acciones con arquitectura multi-agente
- 6 agentes especializados coordinados por un Orchestrator central
- Microservicio Python independiente para análisis NLP con FinBERT
- API REST que expone recomendaciones en tiempo real
- Scheduler automático que ejecuta el análisis cada 4 horas en horario de mercado
- Notificaciones push a Telegram cuando la señal tiene confianza ALTA
- Configuración multi-ticker escalable vía `tickers.yaml`

## Capabilities

### New Capabilities

- `price-data-ingestion`: Obtención y normalización de precios históricos OHLCV desde Alpha Vantage / Yahoo Finance
- `technical-signal-calculation`: Cálculo de indicadores técnicos (RSI 14 períodos, MACD) sobre los precios obtenidos
- `news-ingestion`: Consumo de feeds RSS y NewsAPI para obtener titulares financieros relacionados al ticker en las últimas 24h
- `sentiment-analysis`: Análisis de sentimiento financiero sobre titulares usando el modelo FinBERT (microservicio Python/FastAPI)
- `recommendation-engine`: Motor de decisión que combina señales técnicas y sentimiento para emitir COMPRAR / VENDER / MANTENER con nivel de confianza
- `telegram-notifications`: Envío de alertas formateadas al celular del usuario vía bot de Telegram cuando la confianza es ALTA

### Modified Capabilities

## Impact

- **Nuevo proyecto** — no hay código existente afectado
- **Dependencias externas nuevas:**
  - Alpha Vantage API (datos de precio) — requiere API key gratuita
  - NewsAPI (noticias) — requiere API key gratuita
  - HuggingFace `ProsusAI/finbert` (NLP) — descarga local del modelo (~440 MB)
  - Telegram Bot API — requiere crear bot via @BotFather
- **NuGet:** `Skender.Stock.Indicators`, `Telegram.Bot`
- **Python packages:** `fastapi`, `transformers`, `torch`, `uvicorn`
- **Infraestructura:** requiere que el microservicio Python corra en paralelo al backend C# (mismo host o Docker)
- **Secretos necesarios:** `ALPHA_VANTAGE_KEY`, `NEWS_API_KEY`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`
