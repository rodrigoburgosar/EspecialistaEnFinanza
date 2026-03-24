## ADDED Requirements

### Requirement: Dashboard web con historial de recomendaciones
El sistema SHALL exponer una página web en `/dashboard` que muestre el historial de recomendaciones generadas, con filtros por ticker y tipo de acción.

#### Scenario: Acceso al dashboard sin filtros
- **WHEN** el usuario navega a `/dashboard`
- **THEN** se muestra una tabla con las últimas 50 recomendaciones de todos los tickers, ordenadas por fecha descendente, con columnas: Ticker, Fecha, Acción, Confianza, RSI, Sentimiento, Noticias Analizadas

#### Scenario: Filtrar por ticker
- **WHEN** el usuario selecciona un ticker específico en el filtro
- **THEN** la tabla muestra solo las recomendaciones del ticker seleccionado

#### Scenario: Filtrar por tipo de acción
- **WHEN** el usuario selecciona COMPRAR, VENDER o MANTENER en el filtro de acción
- **THEN** la tabla muestra solo las recomendaciones del tipo seleccionado

#### Scenario: Sin recomendaciones registradas
- **WHEN** la base de datos no tiene recomendaciones aún
- **THEN** el dashboard muestra el mensaje "Aún no hay recomendaciones registradas. El análisis se ejecuta cada 4 horas."

### Requirement: Resumen estadístico por ticker en el dashboard
El sistema SHALL mostrar un resumen por ticker con la última recomendación, RSI actual y tendencia de las últimas 5 señales.

#### Scenario: Resumen visible para cada ticker activo
- **WHEN** hay recomendaciones registradas para un ticker
- **THEN** el dashboard muestra una tarjeta por ticker con: última acción, RSI, sentimiento y los últimos 5 íconos de tendencia (🟢/🔴/⚪)
