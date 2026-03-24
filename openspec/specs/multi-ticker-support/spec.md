## ADDED Requirements

### Requirement: Analizar múltiples tickers en cada ciclo del scheduler
El sistema SHALL iterar sobre todos los tickers marcados como `activo: true` en `config/tickers.yaml` durante cada ciclo de análisis, ejecutando el flujo completo de agentes para cada uno de forma independiente.

#### Scenario: Múltiples tickers activos en configuración
- **WHEN** `tickers.yaml` contiene dos o más entradas con `activo: true`
- **THEN** el TrabajadorAnalisis ejecuta `Orquestador.AnalizarAsync` para cada ticker en el ciclo, y persiste una recomendación independiente por ticker

#### Scenario: Error en un ticker no detiene los demás
- **WHEN** el análisis de un ticker lanza excepción (ej. API no disponible)
- **THEN** el sistema registra el error en el log y continúa con el siguiente ticker sin interrumpir el ciclo

#### Scenario: Ticker marcado como inactivo
- **WHEN** un ticker en `tickers.yaml` tiene `activo: false`
- **THEN** el TrabajadorAnalisis lo omite completamente en el ciclo de análisis

### Requirement: Caché de precios por ticker para respetar límites de API
El sistema SHALL mantener un caché en memoria con TTL de 4 horas por ticker para evitar llamadas duplicadas a Alpha Vantage dentro del mismo ciclo.

#### Scenario: Precio en caché vigente
- **WHEN** se solicitan precios de un ticker que ya fue consultado hace menos de 4 horas
- **THEN** el AgentePrecios retorna los datos del caché sin llamar a la API externa

#### Scenario: Caché expirado
- **WHEN** los datos en caché tienen más de 4 horas de antigüedad
- **THEN** el AgentePrecios realiza una nueva llamada a la API y actualiza el caché
