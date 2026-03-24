## 1. Setup y estructura del proyecto

- [ ] 1.1 Crear solución `StockAnalyzer.sln` con proyectos: Api, Orchestrator, Agents, Contracts, Worker, NLP
- [ ] 1.2 Agregar NuGet `Skender.Stock.Indicators` al proyecto Agents
- [ ] 1.3 Agregar NuGet `Telegram.Bot` al proyecto Agents
- [ ] 1.4 Crear archivo `config/tickers.yaml` con entrada inicial para PLTR
- [ ] 1.5 Crear `.env.example` con variables: `ALPHA_VANTAGE_KEY`, `NEWS_API_KEY`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`
- [ ] 1.6 Configurar `appsettings.json` para leer variables de entorno con `Microsoft.Extensions.Configuration`

## 2. Contratos e interfaces (StockAnalyzer.Contracts)

- [ ] 2.1 Definir modelo `Quote` (fecha, open, high, low, close, volume)
- [ ] 2.2 Definir modelo `TechnicalSignals` (RSI, MACD, clasificación)
- [ ] 2.3 Definir modelo `Recomendacion` (ticker, acción, confianza, RSI, sentimiento, fecha)
- [ ] 2.4 Definir interfaces: `IPriceDataAgent`, `ITechnicalSignalAgent`, `INewsIngestionAgent`, `IDecisionAgent`, `INotificationAgent`

## 3. Price Data Agent

- [ ] 3.1 Implementar `PriceDataAgent` que llama a Alpha Vantage API con `HttpClient`
- [ ] 3.2 Implementar parser de respuesta JSON a colección de `Quote`
- [ ] 3.3 Filtrar y retornar solo los últimos 14 días
- [ ] 3.4 Manejar errores de API (timeout, ticker inválido) con log y excepción controlada

## 4. Technical Signal Agent

- [ ] 4.1 Implementar `TechnicalSignalAgent` usando `Skender.Stock.Indicators`
- [ ] 4.2 Calcular RSI de 14 períodos y clasificar (SOBREVENTA / NEUTRAL / SOBRECOMPRA)
- [ ] 4.3 Calcular MACD y detectar cruce de líneas
- [ ] 4.4 Retornar valor por defecto RSI=50 si hay menos de 14 registros de precio

## 5. News Ingestion Agent

- [ ] 5.1 Implementar consumo de feed RSS de Yahoo Finance con `SyndicationFeed`
- [ ] 5.2 Implementar consumo de NewsAPI con `HttpClient`
- [ ] 5.3 Filtrar titulares que contengan "PLTR" o "Palantir" (case-insensitive)
- [ ] 5.4 Limitar resultado a los 20 titulares más recientes de las últimas 24h
- [ ] 5.5 Manejar fallo de fuente individual sin detener el agente

## 6. Sentiment Analysis Agent (Python / FastAPI)

- [ ] 6.1 Crear proyecto Python en `src/StockAnalyzer.NLP/` con `requirements.txt`
- [ ] 6.2 Instalar dependencias: `fastapi`, `uvicorn`, `transformers`, `torch`
- [ ] 6.3 Implementar carga del modelo `ProsusAI/finbert` al iniciar el servicio
- [ ] 6.4 Implementar `POST /sentiment` que recibe lista de strings y retorna score consolidado
- [ ] 6.5 Retornar score 0.0 si la lista de titulares está vacía
- [ ] 6.6 Agregar health check `GET /health` para que C# verifique disponibilidad

## 7. Decision Agent

- [ ] 7.1 Implementar `DecisionAgent` con tabla de decisión RSI + sentimiento
- [ ] 7.2 Caso COMPRAR: RSI < 30 AND sentimiento > +0.3 → confianza ALTA
- [ ] 7.3 Caso VENDER: RSI > 70 AND sentimiento < -0.3 → confianza ALTA
- [ ] 7.4 Caso MANTENER: todos los demás casos → confianza MEDIA
- [ ] 7.5 Retornar objeto `Recomendacion` completo con todas las métricas

## 8. Notification Agent (Telegram)

- [ ] 8.1 Implementar `NotificationAgent` con `Telegram.Bot`
- [ ] 8.2 Leer `TELEGRAM_BOT_TOKEN` y `TELEGRAM_CHAT_ID` desde configuración
- [ ] 8.3 Deshabilitar agente (log de advertencia) si las variables no están definidas
- [ ] 8.4 Formatear mensaje con emoji, ticker, acción, RSI, sentimiento, confianza y timestamp UTC
- [ ] 8.5 Solo enviar si confianza es ALTA (COMPRAR o VENDER)
- [ ] 8.6 Capturar errores de Telegram sin relanzar excepción al Orchestrator

## 9. Orchestrator

- [ ] 9.1 Implementar `Orchestrator` que coordina todos los agentes en secuencia
- [ ] 9.2 Llamar al microservicio Python via `HttpClient` con fallback a sentimiento neutro si no responde
- [ ] 9.3 Invocar `NotificationAgent` solo si confianza es ALTA
- [ ] 9.4 Registrar resultado de cada ciclo en SQLite con Entity Framework Core

## 10. API REST y Scheduler

- [ ] 10.1 Implementar `GET /api/recommendation/{ticker}` en ASP.NET Core
- [ ] 10.2 Retornar HTTP 404 si el ticker no está en `tickers.yaml`
- [ ] 10.3 Configurar Swagger/OpenAPI para documentación automática
- [ ] 10.4 Implementar `AnalysisWorker` como `BackgroundService` con `PeriodicTimer` de 4 horas
- [ ] 10.5 Restringir ejecución del scheduler a horario de mercado (9:00–17:00 ET, lunes a viernes)

## 11. Persistencia

- [ ] 11.1 Configurar Entity Framework Core con SQLite
- [ ] 11.2 Crear tabla `Recomendaciones` (id, ticker, fecha, rsi, sentimiento, accion, confianza)
- [ ] 11.3 Guardar cada resultado del Orchestrator en base de datos

## 12. Validación y pruebas

- [ ] 12.1 Crear bot de Telegram via @BotFather y obtener token
- [ ] 12.2 Obtener CHAT_ID y configurar variables de entorno
- [ ] 12.3 Probar `NotificationAgent` enviando mensaje manual antes de conectar al scheduler
- [ ] 12.4 Ejecutar análisis manual para PLTR y verificar respuesta de la API
- [ ] 12.5 Verificar que señal MANTENER no genera mensaje en Telegram
- [ ] 12.6 Verificar que fallo del microservicio Python no detiene el sistema C#
