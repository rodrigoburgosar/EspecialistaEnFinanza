## Context

El sistema de Fase 2 funciona correctamente para PLTR con SQLite. Para escalar a producción se requiere: soporte multi-ticker sin cambios de código, base de datos robusta, visibilidad del historial y señales técnicas más completas. Todas las decisiones buscan minimizar el impacto sobre el código existente.

## Goals / Non-Goals

**Goals:**
- Agregar tickers desde configuración sin tocar código
- PostgreSQL con migraciones EF Core versionadas
- Dashboard web integrado en la API existente (sin nuevo servicio)
- Bollinger Bands y EMA como señales de confirmación adicionales
- Modelo de decisión más preciso aprovechando las nuevas señales

**Non-Goals:**
- Autenticación en el dashboard (uso interno)
- Análisis en tiempo real (tick-by-tick)
- Ejecución automática de órdenes de compra/venta
- Soporte para instrumentos distintos a acciones (opciones, crypto)

## Decisions

### 1. Multi-ticker via tickers.yaml sin cambios de código

**Decisión:** `TrabajadorAnalisis` ya lee `tickers.yaml`. Solo se agrega validación de esquema y soporte para múltiples entradas activas.

**Rationale:** El diseño actual ya prevé esto — el Worker itera sobre la lista. Solo hay que asegurarse de que el Orquestador maneje cada ticker de forma independiente y que los errores de un ticker no detengan el ciclo de los demás.

### 2. PostgreSQL con Npgsql + migraciones EF Core

**Decisión:** Reemplazar `UseSqlite` por `UseNpgsql` en `ContextoBd`. Agregar migraciones versionadas con `dotnet ef migrations add`.

**Rationale:** PostgreSQL ofrece concurrencia, índices más eficientes y es el estándar para producción. La transición es mínima gracias a EF Core — solo cambia el provider y la connection string.

**Alternativas consideradas:**
- Mantener SQLite: válido para desarrollo local pero no escala a múltiples instancias
- SQL Server: posible pero agrega costo de licencia innecesario

### 3. Dashboard en Razor Pages integrado en StockAnalyzer.Api

**Decisión:** Agregar Razor Pages a la API existente para el dashboard. No crear un nuevo proyecto.

**Rationale:** Evita overhead de un nuevo proyecto y nuevo puerto. El dashboard es de uso interno y consulta directamente `IRepositorioRecomendaciones`.

**Alternativas consideradas:**
- Blazor Server: más interactivo pero agrega complejidad innecesaria para un historial de solo lectura
- Proyecto separado: mayor aislamiento pero innecesario para MVP del dashboard

### 4. Bollinger Bands y EMA como señales de confirmación

**Decisión:** Calcular Bollinger Bands (20 períodos, 2 desviaciones estándar) y EMA 20 en `AgenteSeñalesTecnicas`. Usarlos en `AgenteDecision` como confirmadores, no como señales primarias.

**Rationale:** RSI sigue siendo la señal principal. Bollinger Bands confirman volatilidad extrema y EMA confirma tendencia. Esto reduce falsos positivos sin cambiar la lógica central.

**Tabla de decisión enriquecida:**
- COMPRAR ALTA: RSI<30 AND sentimiento>0.3 AND precio cerca de banda inferior Bollinger
- VENDER ALTA: RSI>70 AND sentimiento<-0.3 AND precio cerca de banda superior Bollinger
- COMPRAR MEDIA: RSI<30 AND sentimiento>0.3 (sin confirmación Bollinger)
- El resto: MANTENER

## Risks / Trade-offs

| Riesgo | Mitigación |
|---|---|
| PostgreSQL no disponible en entorno de desarrollo | Mantener SQLite como opción via `appsettings.Development.json` |
| Bollinger Bands requiere más historial (20 días mínimo) | Ampliar ventana de precios de 14 a 25 días en `AgentePrecios` |
| Alpha Vantage: límite de 25 req/día con múltiples tickers | Implementar caché en memoria con TTL de 4h por ticker |
| Dashboard sin auth expone datos internos | Aceptable para uso local; agregar auth básica en Fase 4 si se expone externamente |

## Migration Plan

1. Crear migración EF Core inicial para PostgreSQL
2. Actualizar `appsettings.json` con `DATABASE_URL`
3. Extender `SeñalesTecnicas` con nuevos campos (record — cambio no breaking para consumidores internos)
4. Actualizar `AgenteSeñalesTecnicas` con nuevos cálculos
5. Actualizar `AgenteDecision` con tabla enriquecida
6. Agregar Razor Pages para dashboard
7. Actualizar `tickers.yaml` con tickers adicionales

## Open Questions

- ¿Cuántos tickers adicionales además de PLTR se quieren analizar inicialmente?
- ¿El dashboard necesita exportar a CSV o solo visualización web?
- ¿Se necesita un cache distribuido (Redis) para múltiples instancias o es suficiente cache en memoria?
