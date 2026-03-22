## ADDED Requirements

### Requirement: Obtener precios históricos del ticker
El sistema SHALL obtener precios OHLCV (apertura, máximo, mínimo, cierre, volumen) de los últimos 14 días calendario para un ticker dado desde Alpha Vantage o Yahoo Finance.

#### Scenario: Obtención exitosa de precios
- **WHEN** el Orchestrator solicita precios para un ticker válido (ej. "PLTR")
- **THEN** el Price Data Agent retorna una colección de quotes con fecha, open, high, low, close y volume para los últimos 14 días

#### Scenario: API externa no disponible
- **WHEN** la API de precios retorna error o timeout
- **THEN** el agente lanza una excepción que el Orchestrator captura y registra en el log, deteniendo el ciclo de análisis actual sin afectar el siguiente

#### Scenario: Ticker inválido
- **WHEN** se solicita precios para un ticker inexistente
- **THEN** el agente retorna colección vacía y registra advertencia en el log

### Requirement: Normalización del formato de precios
El sistema SHALL normalizar los datos de precio en un modelo interno `Quote` independiente de la fuente (Alpha Vantage o Yahoo Finance) para que el Technical Signal Agent no dependa de la fuente de datos.

#### Scenario: Cambio de proveedor de datos
- **WHEN** se configura Yahoo Finance como fuente en lugar de Alpha Vantage
- **THEN** el Technical Signal Agent recibe exactamente el mismo tipo `Quote` sin modificaciones en su código
