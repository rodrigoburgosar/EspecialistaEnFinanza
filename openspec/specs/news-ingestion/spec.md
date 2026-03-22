# news-ingestion Specification

## Purpose
TBD - created by archiving change stock-analysis-multi-agent. Update Purpose after archive.
## Requirements
### Requirement: Consumir noticias desde RSS y NewsAPI
El sistema SHALL obtener titulares financieros de las últimas 24 horas relacionados con el ticker desde al menos dos fuentes: feeds RSS (Yahoo Finance, Reuters) y NewsAPI.

#### Scenario: Obtención exitosa de titulares
- **WHEN** el Orchestrator solicita noticias para "PLTR"
- **THEN** el News Ingestion Agent retorna una lista de hasta 20 titulares recientes relevantes al ticker

#### Scenario: Fuente RSS no disponible
- **WHEN** un feed RSS específico falla o no responde
- **THEN** el agente continúa con las demás fuentes disponibles y retorna los titulares obtenidos de las fuentes activas

#### Scenario: Sin noticias en las últimas 24 horas
- **WHEN** no se encuentran noticias recientes para el ticker
- **THEN** el agente retorna lista vacía y el Orchestrator asigna sentimiento neutro (0.0) por defecto

### Requirement: Filtrar noticias por relevancia al ticker
El sistema SHALL filtrar los titulares para incluir solo aquellos que mencionen explícitamente el ticker o el nombre de la empresa ("Palantir").

#### Scenario: Titular relevante incluido
- **WHEN** un titular contiene "PLTR" o "Palantir"
- **THEN** el titular es incluido en la lista de resultados

#### Scenario: Titular irrelevante excluido
- **WHEN** un titular no menciona el ticker ni la empresa
- **THEN** el titular es descartado del resultado

