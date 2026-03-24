## Context

No existe sistema previo. Se construye desde cero un sistema multi-agente de análisis financiero para Palantir (PLTR), escalable a múltiples tickers. El usuario necesita recibir alertas en su celular (Telegram) cuando el sistema detecte señales de compra o venta con alta confianza, sin necesidad de monitorear manualmente el mercado.

El sistema debe ser mantenible, con cada agente como unidad independiente con responsabilidad única.

## Goals / Non-Goals

**Goals:**
- Arquitectura multi-agente con separación clara de responsabilidades
- Análisis cada 4 horas en horario de mercado sin intervención manual
- Notificación Telegram solo ante señales de alta confianza (evitar ruido)
- NLP de calidad financiera con FinBERT sin depender de APIs de pago
- Escalable a múltiples tickers modificando solo configuración

**Non-Goals:**
- Ejecución automática de órdenes de compra/venta en brokers
- Análisis en tiempo real (tick-by-tick)
- Dashboard web (Fase 3+)
- Soporte para otros instrumentos financieros (opciones, crypto)

## Decisions

### 1. Arquitectura híbrida C# + Python

**Decisión:** Backend principal en .NET 9 (C#), microservicio NLP en Python.

**Rationale:** C# ofrece tipado estricto, `Skender.Stock.Indicators` para RSI/MACD nativo, y `IHostedService` para scheduling sin dependencias externas. Python es el único ecosistema con FinBERT disponible localmente sin pago a APIs externas.

**Alternativas consideradas:**
- Todo Python: más simple para MVP, pero menor robustez en producción y sin `Skender`
- Todo C# con OpenAI API para NLP: elimina Python pero añade costo por request y dependencia externa
- Todo C# con ML.NET: evita Python pero FinBERT no está disponible, requiere entrenar modelo propio

### 2. Comunicación entre servicios via HTTP REST

**Decisión:** El `NotificationAgent` en C# llama al microservicio Python via `HttpClient` en `POST /sentiment`.

**Rationale:** Simple, stateless, fácil de probar y monitorear. Ambos servicios corren en el mismo host en producción inicial.

**Alternativas consideradas:**
- Message Queue (RabbitMQ): mayor resiliencia pero overhead innecesario para MVP
- gRPC: mejor performance pero mayor complejidad de configuración

### 3. Filtro de notificaciones por confianza ALTA

**Decisión:** El `NotificationAgent` solo dispara cuando `Decision Agent` emite confianza `ALTA`.

**Rationale:** Evitar fatiga de alertas. Un mensaje Telegram por señal neutral no aporta valor y genera desconfianza en el sistema.

**Alternativas consideradas:**
- Notificar todas las señales: genera demasiado ruido
- Notificar con umbral configurable: válido para Fase 3, innecesario ahora

### 4. Scheduler nativo con IHostedService

**Decisión:** `BackgroundService` con `PeriodicTimer` cada 4 horas.

**Rationale:** Nativo en .NET, sin dependencias externas. Suficiente para la frecuencia requerida.

**Alternativas consideradas:**
- Quartz.NET: más potente (cron expressions) pero innecesario para un intervalo fijo
- Hangfire: añade persistencia de jobs pero requiere base de datos adicional

### 5. SQLite para persistencia en MVP

**Decisión:** SQLite para guardar historial de recomendaciones.

**Rationale:** Sin infraestructura adicional, suficiente para un solo nodo. Migración a PostgreSQL planificada para Fase 3.

## Risks / Trade-offs

| Riesgo | Mitigación |
|---|---|
| Alpha Vantage límite 25 req/día en plan free | Cachear precios localmente; en producción migrar a Polygon.io |
| FinBERT lento en CPU (~2-3s por batch) | Aceptable para análisis cada 4h; en producción usar GPU o batch nocturno |
| Microservicio Python caído | Orchestrator captura la excepción y omite análisis de sentimiento; emite MANTENER por defecto |
| Bot Telegram bloqueado o token inválido | Loggear error, no interrumpir el análisis principal |
| RSI y sentimiento contradictorios | Tabla de decisión explícita cubre todos los casos; señal contradictoria → MANTENER |

## Migration Plan

1. **Fase 1 (MVP):** Script Python standalone para validar el concepto — sin C#, sin API REST
2. **Fase 2:** Migrar a arquitectura híbrida C# + microservicio Python con API REST y scheduler
3. **Fase 3:** Notification Agent Telegram + multi-ticker + PostgreSQL

No hay rollback necesario — es proyecto nuevo sin usuarios productivos.

## Open Questions

- ¿Se necesita autenticación en la API REST o es solo uso interno?
- ¿El microservicio Python corre en Docker o directamente en el mismo host que .NET?
- ¿Se quiere histórico de recomendaciones accesible via API o solo almacenamiento interno?
