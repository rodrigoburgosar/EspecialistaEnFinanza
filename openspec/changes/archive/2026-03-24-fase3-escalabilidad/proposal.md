## Why

El sistema actual analiza solo PLTR con un único scheduler y almacena en SQLite sin visibilidad del historial. Para hacerlo production-ready se necesita soporte multi-ticker configurable, migración a PostgreSQL para mayor robustez, un dashboard web que muestre el historial de recomendaciones, e indicadores técnicos adicionales (Bollinger Bands, EMA) que enriquezcan la señal de decisión.

## What Changes

- **Multi-ticker:** el Orquestador y el TrabajadorAnalisis iteran sobre todos los tickers activos en `tickers.yaml`, no solo PLTR
- **PostgreSQL:** reemplazo de SQLite por PostgreSQL como motor de persistencia, con migraciones EF Core
- **Dashboard web:** nueva página Razor/Blazor que muestra el historial de recomendaciones por ticker con filtros de fecha y acción
- **Indicadores adicionales:** Bollinger Bands y EMA (20 períodos) calculados por el AgenteSeñalesTecnicas e incluidos en la decisión
- **Modelo de decisión enriquecido:** el AgenteDecision incorpora Bollinger Bands y EMA como señales de confirmación adicionales al RSI y sentimiento

## Capabilities

### New Capabilities

- `multi-ticker-support`: soporte para analizar múltiples tickers configurables desde `tickers.yaml` en cada ciclo del scheduler
- `postgresql-persistence`: persistencia en PostgreSQL con migraciones EF Core, reemplazando SQLite
- `historial-dashboard`: dashboard web con historial de recomendaciones por ticker, filtrable por fecha y tipo de acción
- `indicadores-adicionales`: cálculo de Bollinger Bands y EMA 20 como indicadores de confirmación de señal

### Modified Capabilities

- `recommendation-engine`: incorpora Bollinger Bands y EMA como señales de confirmación adicionales en la tabla de decisión
- `technical-signal-calculation`: añade cálculo de Bollinger Bands y EMA 20 al conjunto de indicadores retornados

## Impact

- **`config/tickers.yaml`** — se agregan más tickers con flag `activo`
- **`StockAnalyzer.Agentes/AgenteSeñalesTecnicas.cs`** — nuevos indicadores Bollinger Bands y EMA
- **`StockAnalyzer.Contratos/Modelos/SeñalesTecnicas.cs`** — record extendido con BollingerSuperior, BollingerInferior, EMA
- **`StockAnalyzer.Agentes/AgenteDecision.cs`** — tabla de decisión enriquecida con nuevas señales
- **`StockAnalyzer.Api/Datos/ContextoBd.cs`** — migración a PostgreSQL provider
- **`StockAnalyzer.Api/`** — nueva página de dashboard (Blazor o Razor Pages)
- **Nuevas dependencias NuGet:** `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Components.Web` (si Blazor)
- **Nueva variable de entorno:** `DATABASE_URL` (connection string PostgreSQL)
