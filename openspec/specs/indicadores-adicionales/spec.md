## ADDED Requirements

### Requirement: Calcular Bollinger Bands de 20 períodos
El sistema SHALL calcular las bandas de Bollinger (período 20, 2 desviaciones estándar) usando `Skender.Stock.Indicators` y retornarlas como parte de las señales técnicas.

#### Scenario: Precio cerca de banda inferior (señal alcista)
- **WHEN** el precio de cierre actual está dentro del 2% de la banda inferior de Bollinger
- **THEN** `SeñalesTecnicas` incluye `CercaBandaInferior = true`

#### Scenario: Precio cerca de banda superior (señal bajista)
- **WHEN** el precio de cierre actual está dentro del 2% de la banda superior de Bollinger
- **THEN** `SeñalesTecnicas` incluye `CercaBandaSuperior = true`

#### Scenario: Datos insuficientes para Bollinger Bands
- **WHEN** hay menos de 20 cotizaciones disponibles
- **THEN** el sistema retorna `CercaBandaInferior = false` y `CercaBandaSuperior = false` sin lanzar excepción

### Requirement: Calcular EMA de 20 períodos
El sistema SHALL calcular la Media Móvil Exponencial de 20 períodos (EMA 20) y determinar si el precio está por encima o por debajo de ella como señal de tendencia.

#### Scenario: Precio por encima de EMA 20 (tendencia alcista)
- **WHEN** el precio de cierre es mayor que la EMA 20
- **THEN** `SeñalesTecnicas` incluye `TendenciaAlcista = true`

#### Scenario: Precio por debajo de EMA 20 (tendencia bajista)
- **WHEN** el precio de cierre es menor que la EMA 20
- **THEN** `SeñalesTecnicas` incluye `TendenciaAlcista = false`

## MODIFIED Requirements

### Requirement: Generar recomendación combinando RSI y sentimiento
El sistema SHALL evaluar las señales técnicas y el puntaje de sentimiento para emitir una recomendación de COMPRAR, VENDER o MANTENER con nivel de confianza ALTA o MEDIA, incorporando Bollinger Bands y EMA como señales de confirmación.

#### Scenario: Señal de compra con alta confianza (con confirmación Bollinger)
- **WHEN** RSI < 30 AND sentimiento > +0.3 AND precio cerca de banda inferior Bollinger
- **THEN** el Decision Agent emite recomendación "COMPRAR" con confianza "ALTA"

#### Scenario: Señal de compra con confianza media (sin confirmación Bollinger)
- **WHEN** RSI < 30 AND sentimiento > +0.3 AND precio NO está cerca de banda inferior
- **THEN** el Decision Agent emite recomendación "COMPRAR" con confianza "MEDIA"

#### Scenario: Señal de venta con alta confianza (con confirmación Bollinger)
- **WHEN** RSI > 70 AND sentimiento < -0.3 AND precio cerca de banda superior Bollinger
- **THEN** el Decision Agent emite recomendación "VENDER" con confianza "ALTA"

#### Scenario: Señal de venta con confianza media (sin confirmación Bollinger)
- **WHEN** RSI > 70 AND sentimiento < -0.3 AND precio NO está cerca de banda superior
- **THEN** el Decision Agent emite recomendación "VENDER" con confianza "MEDIA"

#### Scenario: Señales contradictorias o neutras
- **WHEN** las señales técnicas y de sentimiento no coinciden, o el RSI está en zona neutral (30-70)
- **THEN** el Decision Agent emite recomendación "MANTENER" con confianza "MEDIA"
