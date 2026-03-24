## ADDED Requirements

### Requirement: Calcular RSI de 14 períodos
El sistema SHALL calcular el RSI (Relative Strength Index) de 14 períodos a partir de los precios de cierre recibidos del Price Data Agent usando `Skender.Stock.Indicators`.

#### Scenario: RSI en zona de sobreventa
- **WHEN** el RSI calculado es menor a 30
- **THEN** el agente retorna señal técnica con valor RSI y clasificación "SOBREVENTA"

#### Scenario: RSI en zona de sobrecompra
- **WHEN** el RSI calculado es mayor a 70
- **THEN** el agente retorna señal técnica con valor RSI y clasificación "SOBRECOMPRA"

#### Scenario: RSI en zona neutral
- **WHEN** el RSI calculado está entre 30 y 70
- **THEN** el agente retorna señal técnica con valor RSI y clasificación "NEUTRAL"

#### Scenario: Datos insuficientes para RSI
- **WHEN** la colección de precios tiene menos de 14 registros
- **THEN** el agente retorna RSI con valor 50 (neutral por defecto) y registra advertencia

### Requirement: Calcular MACD como señal de confirmación
El sistema SHALL calcular el MACD (Moving Average Convergence Divergence) como indicador de confirmación de tendencia.

#### Scenario: MACD confirma señal alcista
- **WHEN** la línea MACD cruza por encima de la línea de señal
- **THEN** el agente incluye confirmación alcista en las señales técnicas retornadas

#### Scenario: MACD confirma señal bajista
- **WHEN** la línea MACD cruza por debajo de la línea de señal
- **THEN** el agente incluye confirmación bajista en las señales técnicas retornadas
