# StockAnalyzer — Sistema Multi-Agente de Análisis Bursátil

> ⚠️ **Proyecto de práctica.** Este sistema genera señales algorítmicas con fines educativos. No constituye asesoría financiera ni recomendación de inversión.

Sistema automatizado que combina indicadores técnicos (RSI, Bollinger Bands, EMA) con análisis de sentimiento de noticias financieras para generar recomendaciones de compra, venta o mantención de acciones. Cada responsabilidad está encapsulada en un agente especializado, coordinados por un Orquestador central.

---

## Arquitectura

```
Orquestador
├── AgentePrecios          → precios OHLCV desde Alpha Vantage (caché 4h)
├── AgenteSeñalesTecnicas  → RSI, MACD, Bollinger Bands, EMA 20
├── AgenteNoticias         → titulares desde RSS y Newsdata.io
├── AgenteSentimiento      → llama al microservicio Python/FinBERT
├── AgenteDecision         → emite COMPRAR / VENDER / MANTENER
└── AgenteNotificacion     → alerta Telegram cuando confianza es ALTA
```

### Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| API y agentes | ASP.NET Core (.NET 9) + C# 12 |
| Indicadores técnicos | Skender.Stock.Indicators |
| Análisis de sentimiento | FastAPI + Python 3.13 + FinBERT |
| Base de datos | SQLite (desarrollo) / PostgreSQL (producción) |
| Dashboard web | Razor Pages |
| Notificaciones | Telegram Bot API |
| Scheduler | IHostedService + PeriodicTimer (cada 4h) |
| Documentación API | Swagger / OpenAPI |

---

## Requisitos previos

### Herramientas

| Herramienta | Versión mínima | Descarga |
|-------------|---------------|---------|
| .NET SDK | 9.0 | https://dotnet.microsoft.com/download |
| Python | 3.11+ | https://www.python.org/downloads |
| Git | cualquiera | https://git-scm.com |
| PostgreSQL *(opcional)* | 15+ | https://www.postgresql.org/download |

### API Keys necesarias

| Servicio | Uso | Plan gratuito |
|----------|-----|--------------|
| [Alpha Vantage](https://www.alphavantage.co/support/#api-key) | Precios históricos OHLCV | 25 req/día |
| [Newsdata.io](https://newsdata.io/register) | Titulares de noticias | 200 req/día |
| [Telegram BotFather](https://t.me/BotFather) | Notificaciones | Gratuito |

---

## Instalación

### 1. Clonar el repositorio

```bash
git clone https://github.com/rodrigoburgosar/EspecialistaEnFinanza.git
cd EspecialistaEnFinanza
```

### 2. Configurar las API keys

Crear el archivo `src/StockAnalyzer.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AlphaVantage": {
    "ApiKey": "TU_API_KEY_ALPHAVANTAGE"
  },
  "NewsApi": {
    "ApiKey": "TU_API_KEY_NEWSDATA"
  },
  "Telegram": {
    "BotToken": "TU_BOT_TOKEN",
    "ChatId": "TU_CHAT_ID"
  },
  "ConnectionStrings": {
    "Postgres": ""
  }
}
```

> **Cómo obtener el Chat ID de Telegram:**
> 1. Creá un bot con [@BotFather](https://t.me/BotFather) y copiá el token
> 2. Mandá `/start` a tu bot
> 3. Abrí `https://api.telegram.org/bot<TU_TOKEN>/getUpdates` en el navegador
> 4. Buscá `"chat":{"id": XXXXXXX}` — ese es tu Chat ID

### 3. Compilar y ejecutar la API

```bash
# Compilar
dotnet build

# Ejecutar la API
dotnet run --project src/StockAnalyzer.Api
```

La API queda disponible en `http://localhost:5096`.

### 4. Instalar y ejecutar el microservicio NLP (opcional)

El microservicio de análisis de sentimiento requiere descargar el modelo FinBERT (~500 MB la primera vez).

```bash
cd src/StockAnalyzer.NLP
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

> Sin el microservicio NLP activo, el sistema usa sentimiento neutro (0.0) como fallback y sigue funcionando.

---

## Uso

### Dashboard web

Abrí `http://localhost:5096/dashboard` para ver el historial de recomendaciones con filtros por ticker y acción.

### Disparar un análisis manualmente

```bash
curl -X POST http://localhost:5096/api/analisis/PLTR
```

Respuesta de ejemplo:

```json
{
  "ticker": "PLTR",
  "accion": "MANTENER",
  "confianza": "MEDIA",
  "rsi": 68.5,
  "sentimiento": -0.22,
  "noticiasAnalizadas": 3,
  "fecha": "2026-03-24T02:00:00Z"
}
```

### Probar notificación Telegram

```bash
curl -X POST http://localhost:5096/api/notificacion/test
```

### Swagger UI

Documentación interactiva de la API en `http://localhost:5096/swagger`.

### Tickers configurados

Los tickers activos se definen en `config/tickers.yaml`:

```yaml
tickers:
  - simbolo: PLTR   # Palantir Technologies
  - simbolo: MSFT   # Microsoft
  - simbolo: NVDA   # NVIDIA
  - simbolo: GOOGL  # Alphabet
```

El Worker analiza todos los tickers activos cada 4 horas automáticamente.

---

## Tabla de decisión

| RSI | Sentimiento | Bollinger | Confianza | Recomendación |
|-----|-------------|-----------|-----------|--------------|
| < 30 | > +0.3 | Cerca banda inferior | **ALTA** | **COMPRAR** |
| < 30 | > +0.3 | Sin confirmación | MEDIA | COMPRAR |
| > 70 | < -0.3 | Cerca banda superior | **ALTA** | **VENDER** |
| > 70 | < -0.3 | Sin confirmación | MEDIA | VENDER |
| Cualquiera | Cualquiera | — | MEDIA | MANTENER |

> Las notificaciones Telegram solo se envían cuando la confianza es **ALTA**.

---

## Tests

```bash
# Ejecutar todos los tests
dotnet test

# Solo tests unitarios
dotnet test tests/StockAnalyzer.Tests

# Test específico por nombre
dotnet test tests/StockAnalyzer.Tests --filter "NombreDelTest"

# Tests de una clase
dotnet test tests/StockAnalyzer.Tests --filter "FullyQualifiedName~AgenteDecisionTests"
```

---

## Estructura del proyecto

```
StockAnalyzer.sln
├── src/
│   ├── StockAnalyzer.Api/          # ASP.NET Core — API REST + Dashboard + Swagger
│   ├── StockAnalyzer.Orquestador/  # Coordinación de agentes
│   ├── StockAnalyzer.Agentes/      # Implementación de los agentes C#
│   ├── StockAnalyzer.Contratos/    # Interfaces y modelos compartidos
│   ├── StockAnalyzer.Worker/       # Scheduler — análisis automático cada 4h
│   └── StockAnalyzer.NLP/          # Microservicio Python — FinBERT
├── tests/
│   └── StockAnalyzer.Tests/        # Tests unitarios (xUnit + FluentAssertions)
└── config/
    └── tickers.yaml                # Tickers activos
```

---

## Limitaciones conocidas

- Alpha Vantage plan gratuito: 25 requests/día — suficiente para desarrollo
- FinBERT corre en CPU por defecto (~2–3 seg por análisis); una GPU acelera significativamente
- El feed RSS de Yahoo Finance puede devolver 429 — el sistema continúa con Newsdata.io como fallback
- Las migraciones están optimizadas para SQLite en desarrollo; para PostgreSQL en producción asegurate de tener la connection string configurada

---

## Licencia

Proyecto de práctica — sin licencia comercial.
