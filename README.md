# 📈 StockAnalyzer — Sistema Multi-Agente de Análisis Bursátil

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Python](https://img.shields.io/badge/Python-3.13-3776AB?logo=python)
![License](https://img.shields.io/badge/licencia-práctica-orange)
![Estado](https://img.shields.io/badge/estado-funcional-brightgreen)

> ⚠️ **Proyecto de práctica educativa.** Este sistema genera señales algorítmicas con fines de aprendizaje. No constituye asesoría financiera ni recomendación de inversión real.

Sistema automatizado que combina indicadores técnicos **(RSI, Bollinger Bands, EMA)** con análisis de sentimiento de noticias financieras usando **FinBERT** para generar recomendaciones de compra, venta o mantención de acciones. Cada responsabilidad está encapsulada en un agente especializado, coordinados por un Orquestador central.

---

## ✨ Características

- 🤖 **Arquitectura multi-agente** — cada agente tiene una sola responsabilidad
- 📊 **Indicadores técnicos** — RSI, MACD, Bollinger Bands, EMA 20
- 📰 **Análisis de noticias** — titulares en tiempo real vía Newsdata.io
- 🧠 **Sentimiento con IA** — modelo FinBERT (HuggingFace) para análisis financiero
- 📱 **Alertas Telegram** — notificaciones automáticas cuando la confianza es ALTA
- 🗂️ **Dashboard web** — historial con filtros por ticker y acción
- ⏰ **Análisis automático** — scheduler cada 4 horas
- 🔄 **Multi-ticker** — analiza PLTR, MSFT, NVDA, GOOGL en paralelo
- 💾 **Persistencia** — SQLite en desarrollo, PostgreSQL en producción

---

## 🏗️ Arquitectura

```
┌─────────────────────────────────────────────────────────┐
│                      ORQUESTADOR                         │
│         Coordina el flujo entre todos los agentes        │
└──┬──────────┬──────────┬──────────┬──────────┬──────────┘
   │          │          │          │          │
   ▼          ▼          ▼          ▼          ▼
Precios   Señales    Noticias  Sentimiento  Decisión
OHLCV     Técnicas   RSS +     FinBERT     COMPRAR /
Alpha     RSI/MACD   Newsdata  Python      VENDER /
Vantage   Bollinger  .io       FastAPI     MANTENER
                                               │
                                    confianza ALTA
                                               │
                                               ▼
                                        Notificación
                                          Telegram
```

### Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| API y agentes | ASP.NET Core (.NET 9) + C# 12 |
| Indicadores técnicos | Skender.Stock.Indicators |
| Análisis de sentimiento | FastAPI + Python 3.13 + FinBERT |
| Base de datos | SQLite (dev) / PostgreSQL (prod) |
| ORM | Entity Framework Core 9 |
| Dashboard web | Razor Pages |
| Notificaciones | Telegram Bot API |
| Scheduler | IHostedService + PeriodicTimer |
| Documentación API | Swagger / OpenAPI |
| Tests | xUnit + FluentAssertions + NSubstitute |

---

## 📋 Requisitos previos

### Herramientas

| Herramienta | Versión | Descarga |
|-------------|---------|---------|
| .NET SDK | 9.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Python | 3.11+ | [python.org](https://www.python.org/downloads) |
| Git | cualquiera | [git-scm.com](https://git-scm.com) |
| PostgreSQL | 15+ *(opcional)* | [postgresql.org](https://www.postgresql.org/download) |

### API Keys

| Servicio | Para qué sirve | Plan gratuito |
|----------|---------------|--------------|
| [Alpha Vantage](https://www.alphavantage.co/support/#api-key) | Precios históricos OHLCV | 25 req/día |
| [Newsdata.io](https://newsdata.io/register) | Titulares de noticias financieras | 200 req/día |
| [Telegram BotFather](https://t.me/BotFather) | Alertas push al celular | Gratuito |

---

## 🚀 Instalación

### 1. Clonar el repositorio

```bash
git clone https://github.com/rodrigoburgosar/EspecialistaEnFinanza.git
cd EspecialistaEnFinanza
```

### 2. Configurar variables de entorno

Crear `src/StockAnalyzer.Api/appsettings.Development.json`:

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

<details>
<summary>📱 ¿Cómo obtener tu Chat ID de Telegram?</summary>

1. Creá un bot con [@BotFather](https://t.me/BotFather) y copiá el token
2. Buscá tu bot en Telegram y mandá `/start`
3. Abrí en el navegador: `https://api.telegram.org/bot<TU_TOKEN>/getUpdates`
4. En el JSON buscá `"chat":{"id": XXXXXXX}` — ese número es tu Chat ID

</details>

### 3. Ejecutar la API

```bash
# Compilar
dotnet build

# Ejecutar
dotnet run --project src/StockAnalyzer.Api
```

La API queda disponible en **http://localhost:5096**

### 4. Ejecutar el microservicio NLP *(opcional pero recomendado)*

> Descarga el modelo FinBERT ~500 MB la primera vez

```bash
cd src/StockAnalyzer.NLP
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

> Sin el microservicio NLP activo, el sistema usa sentimiento neutro `0.0` como fallback y sigue funcionando normalmente.

---

## 📖 Uso

### Dashboard web

```
http://localhost:5096/dashboard
```

Muestra tarjetas por ticker con la última señal, RSI, sentimiento e historial de las últimas 50 recomendaciones con filtros.

### Disparar un análisis manualmente

```bash
curl -X POST http://localhost:5096/api/analisis/PLTR
```

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

```
http://localhost:5096/swagger
```

### Tickers configurados

Editá `config/tickers.yaml` para agregar o quitar tickers:

```yaml
tickers:
  - simbolo: PLTR    # Palantir Technologies
    activo: true
  - simbolo: MSFT    # Microsoft
    activo: true
  - simbolo: NVDA    # NVIDIA
    activo: true
  - simbolo: GOOGL   # Alphabet
    activo: true
```

---

## 🧮 Tabla de decisión

| RSI | Sentimiento | Bollinger | Confianza | Recomendación |
|-----|-------------|-----------|-----------|--------------|
| < 30 | > +0.3 | Cerca banda inferior | **ALTA** | 🟢 **COMPRAR** |
| < 30 | > +0.3 | Sin confirmación | MEDIA | 🟢 COMPRAR |
| > 70 | < -0.3 | Cerca banda superior | **ALTA** | 🔴 **VENDER** |
| > 70 | < -0.3 | Sin confirmación | MEDIA | 🔴 VENDER |
| 30–70 | Cualquiera | — | MEDIA | ⚪ MANTENER |

> Las alertas Telegram solo se envían cuando la confianza es **ALTA**

---

## 🧪 Tests

```bash
# Todos los tests
dotnet test

# Solo tests unitarios
dotnet test tests/StockAnalyzer.Tests

# Test específico
dotnet test tests/StockAnalyzer.Tests --filter "NombreDelTest"

# Tests de una clase
dotnet test tests/StockAnalyzer.Tests --filter "FullyQualifiedName~AgenteDecisionTests"

# Con salida detallada
dotnet test tests/StockAnalyzer.Tests -v normal
```

---

## 📁 Estructura del proyecto

```
EspecialistaEnFinanza/
├── src/
│   ├── StockAnalyzer.Api/           # ASP.NET Core — API REST + Dashboard + Swagger
│   │   ├── Controllers/             # RecomendacionController, NotificacionController
│   │   ├── Datos/                   # DbContext, Repositorio, Entidades EF Core
│   │   ├── Migrations/              # Migraciones de base de datos
│   │   └── Pages/Dashboard/         # Razor Pages — Dashboard web
│   ├── StockAnalyzer.Orquestador/   # Coordinación secuencial de agentes
│   ├── StockAnalyzer.Agentes/       # AgentePrecios, Señales, Noticias, Decisión, etc.
│   ├── StockAnalyzer.Contratos/     # Interfaces y modelos compartidos
│   ├── StockAnalyzer.Worker/        # Scheduler — análisis automático cada 4h
│   └── StockAnalyzer.NLP/           # Microservicio Python — FinBERT / FastAPI
├── tests/
│   └── StockAnalyzer.Tests/         # Tests unitarios
└── config/
    └── tickers.yaml                 # Tickers activos
```

---

## ⚠️ Limitaciones conocidas

- **Alpha Vantage gratuito:** 25 req/día — suficiente para desarrollo y pruebas
- **FinBERT en CPU:** ~2–3 segundos por análisis; una GPU lo acelera significativamente
- **RSS Yahoo Finance:** puede devolver 429 (rate limit) — el sistema usa Newsdata.io como fuente principal con fallback automático
- **Migraciones:** optimizadas para SQLite en desarrollo; para PostgreSQL en producción configurar la connection string en `appsettings.json`

---

## 🎓 Sobre este proyecto

Proyecto desarrollado como ejercicio práctico para explorar:
- Arquitectura multi-agente en .NET
- Integración de modelos de NLP (FinBERT) con backends C#
- Comunicación entre microservicios (C# ↔ Python)
- Patrones de diseño: Repository, Dependency Injection, IHostedService
- Testing con xUnit, FluentAssertions y NSubstitute
