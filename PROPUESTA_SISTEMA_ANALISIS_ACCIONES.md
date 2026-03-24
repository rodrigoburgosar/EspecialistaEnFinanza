# Propuesta: Sistema de Análisis y Recomendación de Acciones
**Versión:** 2.0
**Fecha:** 2026-03-22
**Activo inicial:** Palantir Technologies Inc. (`$PLTR`)

---

## 1. Resumen Ejecutivo

Sistema multi-agente automatizado que combina indicadores técnicos (RSI) con análisis de sentimiento de noticias financieras para generar recomendaciones de compra, venta o mantención de acciones. Cada responsabilidad está encapsulada en un agente especializado, coordinados por un Orchestrator central. Diseñado para ser escalable a múltiples tickers.

---

## 2. Arquitectura Multi-Agente

```
                    ┌──────────────────────────────────┐
                    │           ORCHESTRATOR            │
                    │  Coordina, agrega y decide        │
                    │  el flujo entre todos los agentes │
                    └───┬──────┬────────┬──────┬───────┘
                        │      │        │      │
           ┌────────────┘      │        │      └──────────────────┐
           │                   │        │                         │
  ┌────────▼────────┐ ┌────────▼──────┐ │ ┌──────────────────────▼──┐
  │ Price Data Agent│ │Technical Signal│ │ │     Decision Agent      │
  │                 │ │     Agent      │ │ │                         │
  │ Obtiene precios │ │                │ │ │ Evalúa señales          │
  │ históricos del  │ │ Calcula RSI,   │ │ │ combinadas y emite      │
  │ ticker desde    │ │ MACD, Volumen  │ │ │ COMPRAR / VENDER /      │
  │ APIs externas   │ │ a partir de    │ │ │ MANTENER                │
  │                 │ │ los precios    │ │ │          │              │
  └─────────────────┘ └───────────────┘ │ └──────────┼─────────────┘
                                        │            │ (si ALTA)
                    ┌───────────────────┴──────────┐ │
                    │                               │ ▼
          ┌─────────▼──────────┐    ┌──────────────▼────────────┐
          │ News Ingestion Agent│    │   Notification Agent      │
          │                    │    │                           │
          │ Consume RSS, APIs  │    │ Envía alerta a Telegram   │
          │ y feeds de noticias│    │ cuando hay señal ALTA     │
          │ relacionadas con   │    │ 🟢 COMPRAR / 🔴 VENDER    │
          │ el ticker          │    │                           │
          └─────────┬──────────┘    └───────────────────────────┘
                    │
          ┌─────────▼──────────┐
          │ Sentiment Analysis │
          │       Agent        │
          │                    │
          │ Analiza sentimiento│
          │ financiero con     │
          │ FinBERT (Python)   │
          └────────────────────┘
```

---

## 3. Responsabilidad de Cada Agente

### Orchestrator
- Punto de entrada del sistema
- Invoca a cada agente en el orden correcto
- Agrega los resultados de todos los agentes
- Delega la decisión final al **Decision Agent**
- Expone el resultado final vía API REST
- Gestiona el scheduler de ejecución periódica

### Price Data Agent
- Conecta con APIs de datos financieros (Alpha Vantage, Yahoo Finance)
- Retorna precios OHLCV (apertura, máximo, mínimo, cierre, volumen) de los últimos N días
- Normaliza el formato de respuesta independiente de la fuente

### Technical Signal Agent
- Recibe los precios del **Price Data Agent**
- Calcula indicadores técnicos: RSI, MACD, volumen promedio
- Retorna señales técnicas con su nivel de intensidad

### News Ingestion Agent
- Consume fuentes RSS y APIs de noticias (NewsAPI, Yahoo Finance, Reuters)
- Filtra noticias relevantes para el ticker en las últimas 24h
- Retorna lista de titulares y resúmenes limpios

### Sentiment Analysis Agent
- Recibe los titulares del **News Ingestion Agent**
- Aplica modelo FinBERT para análisis de sentimiento financiero
- Retorna un score consolidado entre -1 (negativo) y +1 (positivo)

### Decision Agent
- Recibe señales técnicas + score de sentimiento
- Aplica la tabla de decisión
- Emite la recomendación final con nivel de confianza
- Si la confianza es ALTA, gatilla al **Notification Agent**

### Notification Agent
- Recibe la recomendación del **Orchestrator**
- Solo actúa cuando la confianza es **ALTA** (evita ruido)
- Envía mensaje formateado al bot de Telegram del usuario
- Mensaje incluye: ticker, acción, RSI, sentimiento, confianza y timestamp

---

## 4. Flujo de Datos End-to-End

```
[ORCHESTRATOR] activado por scheduler (cada 4h)
        │
        ├──▶ [Price Data Agent]
        │         │ precios OHLCV últimos 14 días
        │         ▼
        ├──▶ [Technical Signal Agent]
        │         │ RSI, MACD, señales técnicas
        │         │
        ├──▶ [News Ingestion Agent]
        │         │ titulares últimas 24h
        │         ▼
        │    [Sentiment Analysis Agent]
        │         │ score sentimiento -1 a +1
        │         │
        └──▶ [Decision Agent]
                  │ señales técnicas + sentimiento
                  ▼
            COMPRAR / VENDER / MANTENER
                  │
                  ├──▶ [API REST] + [Base de Datos]
                  │
                  └── si confianza ALTA ──▶ [Notification Agent]
                                                    │
                                                    ▼
                                          📱 Mensaje Telegram
                                          🟢 PLTR — COMPRAR
                                          RSI: 27.3 | Sent: +0.71
                                          Confianza: ALTA
```

---

## 5. Tabla de Decisión — Decision Agent

| RSI | Sentimiento | Confianza | Recomendación |
|---|---|---|---|
| < 30 | > +0.3 | ALTA | **COMPRAR** |
| < 30 | Neutro (-0.3 a +0.3) | MEDIA | MANTENER |
| < 30 | < -0.3 | ALTA | MANTENER |
| 30–70 | Cualquiera | — | MANTENER |
| > 70 | > +0.3 | MEDIA | MANTENER |
| > 70 | Neutro | BAJA | MANTENER |
| > 70 | < -0.3 | ALTA | **VENDER** |

---

## 6. Arquitectura Técnica: Híbrida C# + Python

| Agente | Lenguaje | Justificación |
|---|---|---|
| Orchestrator | **C# / ASP.NET Core** | Robustez, tipado, manejo de concurrencia |
| Price Data Agent | **C#** | `Skender.Stock.Indicators` + `HttpClient` |
| Technical Signal Agent | **C#** | `Skender.Stock.Indicators` — RSI/MACD nativos |
| News Ingestion Agent | **C#** | `SyndicationFeed` (RSS nativo) + `HttpClient` |
| Sentiment Analysis Agent | **Python** | FinBERT / HuggingFace — ecosistema NLP maduro |
| Decision Agent | **C#** | Lógica de negocio pura, tipado estricto |
| Notification Agent | **C#** | `Telegram.Bot` (NuGet) — integración simple y directa |

### Comunicación entre servicios

```
┌─────────────────────────────────┐        ┌─────────────────────┐
│     ASP.NET Core (.NET 9)       │        │  FastAPI (Python)   │
│                                 │        │                     │
│  Orchestrator                   │◀──────▶│  Sentiment Analysis │
│  Price Data Agent               │  HTTP  │  Agent              │
│  Technical Signal Agent         │  REST  │  (FinBERT)          │
│  News Ingestion Agent           │        │                     │
│  Decision Agent                 │        └─────────────────────┘
└─────────────────────────────────┘
```

---

## 7. Ejemplos de Código por Agente

### Price Data Agent (C#)
```csharp
public class PriceDataAgent : IPriceDataAgent
{
    public async Task<IEnumerable<Quote>> ObtenerPreciosAsync(string ticker, int dias = 14)
    {
        var response = await _httpClient.GetAsync(
            $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={ticker}&apikey={_apiKey}");
        // parsear y retornar últimos N días
        return _parser.ParsearDiario(await response.Content.ReadAsStringAsync(), dias);
    }
}
```

### Technical Signal Agent (C#)
```csharp
public class TechnicalSignalAgent : ITechnicalSignalAgent
{
    public TechnicalSignals CalcularSeñales(IEnumerable<Quote> precios)
    {
        var rsi  = precios.GetRsi(14).Last().Rsi ?? 50;
        var macd = precios.GetMacd().Last();
        return new TechnicalSignals { RSI = rsi, MACD = macd.Macd ?? 0 };
    }
}
```

### News Ingestion Agent (C#)
```csharp
public class NewsIngestionAgent : INewsIngestionAgent
{
    public async Task<IEnumerable<string>> ObtenerTitularesAsync(string ticker)
    {
        var feed = SyndicationFeed.Load(XmlReader.Create(
            $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={ticker}"));
        return feed.Items.Select(i => i.Title.Text).Take(20);
    }
}
```

### Sentiment Analysis Agent (Python / FastAPI)
```python
from transformers import pipeline
from fastapi import FastAPI

app = FastAPI()
nlp = pipeline("sentiment-analysis", model="ProsusAI/finbert")

@app.post("/sentiment")
def analizar(titulares: list[str]) -> dict:
    resultados = nlp(titulares)
    scores = [r["score"] if r["label"] == "positive" else -r["score"]
              for r in resultados]
    return {"score": sum(scores) / len(scores)}
```

### Decision Agent (C#)
```csharp
public class DecisionAgent : IDecisionAgent
{
    public Recomendacion Evaluar(TechnicalSignals señales, double sentimiento)
    {
        if (señales.RSI < 30 && sentimiento > 0.3)
            return new Recomendacion("COMPRAR", "ALTA");
        if (señales.RSI > 70 && sentimiento < -0.3)
            return new Recomendacion("VENDER", "ALTA");
        return new Recomendacion("MANTENER", "MEDIA");
    }
}
```

### Orchestrator (C#)
```csharp
public class Orchestrator
{
    public async Task<Recomendacion> AnalizarAsync(string ticker)
    {
        var precios     = await _priceAgent.ObtenerPreciosAsync(ticker);
        var señales     = _signalAgent.CalcularSeñales(precios);
        var titulares   = await _newsAgent.ObtenerTitularesAsync(ticker);
        var sentimiento = await _sentimentClient.AnalizarAsync(titulares);
        var resultado   = _decisionAgent.Evaluar(señales, sentimiento.Score);

        // Notificar solo si hay señal accionable con confianza ALTA
        if (resultado.Confianza == "ALTA")
            await _notificationAgent.NotificarAsync(ticker, resultado);

        return resultado;
    }
}
```

### Notification Agent (C#)
```csharp
public class NotificationAgent : INotificationAgent
{
    public async Task NotificarAsync(string ticker, Recomendacion rec)
    {
        var emoji   = rec.Accion == "COMPRAR" ? "🟢" : "🔴";
        var mensaje = $"{emoji} *{ticker}* — {rec.Accion}\n"
                    + $"RSI: {rec.RSI:F1} | Sentimiento: {rec.Sentimiento:F2}\n"
                    + $"Confianza: {rec.Confianza}\n"
                    + $"_{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC_";

        await _telegramClient.EnviarMensajeAsync(mensaje);
    }
}
```

---

## 8. Estructura de Proyecto

```
StockAnalyzer.sln
├── src/
│   ├── StockAnalyzer.Api/                  # ASP.NET Core — API REST + Swagger
│   │   └── Controllers/
│   │       └── RecommendationController.cs
│   ├── StockAnalyzer.Orchestrator/         # Orchestrator — coordinación de agentes
│   │   └── Orchestrator.cs
│   ├── StockAnalyzer.Agents/               # Agentes C#
│   │   ├── PriceDataAgent.cs
│   │   ├── TechnicalSignalAgent.cs
│   │   ├── NewsIngestionAgent.cs
│   │   ├── DecisionAgent.cs
│   │   └── NotificationAgent.cs
│   ├── StockAnalyzer.Contracts/            # interfaces y modelos compartidos
│   │   ├── IPriceDataAgent.cs
│   │   ├── ITechnicalSignalAgent.cs
│   │   ├── INewsIngestionAgent.cs
│   │   ├── IDecisionAgent.cs
│   │   ├── INotificationAgent.cs
│   │   └── Models/
│   ├── StockAnalyzer.Worker/               # IHostedService — scheduler
│   └── StockAnalyzer.NLP/                  # Microservicio Python
│       ├── main.py                         # FastAPI app
│       ├── sentiment_agent.py              # Sentiment Analysis Agent
│       └── requirements.txt
├── config/
│   └── tickers.yaml                        # tickers activos (escalabilidad)
├── .env.example
├── CLAUDE.md
└── PROPUESTA_SISTEMA_ANALISIS_ACCIONES.md
```

---

## 9. Stack Tecnológico

| Capa | Tecnología |
|---|---|
| Orquestación y agentes C# | ASP.NET Core (.NET 9) |
| Indicadores técnicos | `Skender.Stock.Indicators` (NuGet) |
| RSS noticias | `System.ServiceModel.Syndication` (nativo) |
| Agente NLP | FastAPI + Python 3.11 |
| Modelo sentimiento | `ProsusAI/finbert` (HuggingFace) |
| Datos de precio | Alpha Vantage API |
| Noticias | NewsAPI + RSS |
| Base de datos | SQLite (MVP) → PostgreSQL (producción) |
| Scheduler | `IHostedService` + `PeriodicTimer` |
| Notificaciones | `Telegram.Bot` (NuGet) |
| Documentación API | Swagger / OpenAPI |
| Secretos | `.env` + `appsettings.json` |

---

## 10. Plan de Implementación por Fases

### Fase 1 — MVP Python (validación rápida del concepto)
- [ ] `PriceDataAgent` con `yfinance`
- [ ] `TechnicalSignalAgent` con cálculo RSI manual
- [ ] `NewsIngestionAgent` consumiendo RSS de Yahoo Finance
- [ ] `SentimentAnalysisAgent` con FinBERT
- [ ] `DecisionAgent` con tabla de reglas
- [ ] Script CLI que imprima la recomendación final

### Fase 2 — Arquitectura multi-agente C# + Python
- [ ] Migrar agentes C# a ASP.NET Core
- [ ] `SentimentAnalysisAgent` como microservicio FastAPI
- [ ] `Orchestrator` coordinando todos los agentes
- [ ] `IHostedService` como scheduler (cada 4h)
- [ ] API REST con Swagger
- [ ] Persistencia en SQLite

### Fase 3 — Notificaciones Telegram + Escalabilidad

#### Notification Agent — pasos de configuración
- [ ] Crear bot en Telegram via [@BotFather](https://t.me/BotFather) → obtener `BOT_TOKEN`
- [ ] Obtener tu `CHAT_ID` (enviar mensaje al bot y consultar `getUpdates`)
- [ ] Agregar `BOT_TOKEN` y `CHAT_ID` al `.env`
- [ ] Implementar `NotificationAgent` con `Telegram.Bot` (NuGet)
- [ ] Integrar en `Orchestrator` — solo dispara si confianza es ALTA
- [ ] Probar notificación manual antes de conectar al scheduler

#### Escalabilidad
- [ ] Soporte multi-ticker vía `tickers.yaml`
- [ ] Migración a PostgreSQL
- [ ] Dashboard de historial de recomendaciones
- [ ] Indicadores adicionales: Bollinger Bands, EMA
- [ ] Circuit breaker si algún agente falla

---

## 11. Consideraciones

- **APIs gratuitas tienen límites:** Alpha Vantage permite 25 req/día en plan free. Para producción considerar Polygon.io.
- **FinBERT requiere GPU** para inferencia rápida. En CPU es funcional pero más lento (~2–3 seg por batch).
- **Aislamiento de agentes:** cada agente puede fallar independientemente sin tumbar el sistema completo.
- **No es asesoría financiera:** el sistema genera señales algorítmicas, no reemplaza análisis humano.
- **Escalabilidad:** agregar un nuevo ticker es solo añadirlo a `tickers.yaml` — el Orchestrator lo gestiona automáticamente.
