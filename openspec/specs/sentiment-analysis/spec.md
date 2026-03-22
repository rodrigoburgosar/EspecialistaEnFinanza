# sentiment-analysis Specification

## Purpose
TBD - created by archiving change stock-analysis-multi-agent. Update Purpose after archive.
## Requirements
### Requirement: Analizar sentimiento financiero con FinBERT
El sistema SHALL analizar el sentimiento de una lista de titulares usando el modelo `ProsusAI/finbert` expuesto como microservicio FastAPI en Python, retornando un score consolidado entre -1.0 (muy negativo) y +1.0 (muy positivo).

#### Scenario: Titulares con sentimiento positivo
- **WHEN** la mayoría de los titulares analizados tienen tono positivo (ej. "Palantir beats earnings")
- **THEN** el agente retorna un score mayor a +0.3

#### Scenario: Titulares con sentimiento negativo
- **WHEN** la mayoría de los titulares analizados tienen tono negativo (ej. "Palantir misses revenue targets")
- **THEN** el agente retorna un score menor a -0.3

#### Scenario: Lista de titulares vacía
- **WHEN** el News Ingestion Agent retorna lista vacía
- **THEN** el Sentiment Analysis Agent retorna score 0.0 (neutro) sin llamar al modelo

#### Scenario: Microservicio Python no disponible
- **WHEN** el endpoint `/sentiment` del microservicio FastAPI no responde
- **THEN** el Orchestrator asigna sentimiento neutro (0.0) y registra el error, continuando el análisis con solo señales técnicas

### Requirement: Exponer endpoint REST para análisis de sentimiento
El microservicio Python SHALL exponer `POST /sentiment` que acepta una lista de titulares y retorna el score consolidado.

#### Scenario: Llamada exitosa al endpoint
- **WHEN** C# envía `POST /sentiment` con lista de strings
- **THEN** el microservicio retorna `{ "score": <float>, "total_analizados": <int> }` con HTTP 200

