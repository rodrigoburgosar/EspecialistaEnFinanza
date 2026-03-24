## ADDED Requirements

### Requirement: Generar recomendación combinando RSI y sentimiento
El sistema SHALL evaluar las señales técnicas y el score de sentimiento para emitir una recomendación de COMPRAR, VENDER o MANTENER con nivel de confianza ALTA o MEDIA.

#### Scenario: Señal de compra con alta confianza
- **WHEN** el RSI es menor a 30 (sobreventa) Y el sentimiento es mayor a +0.3 (positivo)
- **THEN** el Decision Agent emite recomendación "COMPRAR" con confianza "ALTA"

#### Scenario: Señal de venta con alta confianza
- **WHEN** el RSI es mayor a 70 (sobrecompra) Y el sentimiento es menor a -0.3 (negativo)
- **THEN** el Decision Agent emite recomendación "VENDER" con confianza "ALTA"

#### Scenario: Señales contradictorias o neutras
- **WHEN** las señales técnicas y de sentimiento no coinciden, o el RSI está en zona neutral (30-70)
- **THEN** el Decision Agent emite recomendación "MANTENER" con confianza "MEDIA"

#### Scenario: RSI en sobreventa pero sentimiento negativo
- **WHEN** el RSI es menor a 30 Y el sentimiento es menor a -0.3
- **THEN** el Decision Agent emite recomendación "MANTENER" (señales contradictorias)

### Requirement: Exponer recomendación via API REST
El sistema SHALL exponer `GET /api/recommendation/{ticker}` que retorna la última recomendación calculada con sus métricas.

#### Scenario: Consulta de recomendación existente
- **WHEN** se llama `GET /api/recommendation/PLTR`
- **THEN** el sistema retorna JSON con ticker, fecha, RSI, sentimiento, recomendación, confianza y cantidad de noticias analizadas con HTTP 200

#### Scenario: Ticker no configurado
- **WHEN** se consulta un ticker que no está en `tickers.yaml`
- **THEN** el sistema retorna HTTP 404 con mensaje descriptivo
