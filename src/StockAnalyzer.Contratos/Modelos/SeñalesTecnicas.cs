namespace StockAnalyzer.Contratos.Modelos;

/// <summary>
/// Contiene los indicadores técnicos calculados a partir de los precios históricos:
/// RSI, MACD, Bollinger Bands (20 períodos, 2σ) y EMA 20.
/// </summary>
/// <param name="RSI">Valor del RSI en escala 0–100.</param>
/// <param name="ClasificacionRSI">Clasificación textual: SOBREVENTA, NEUTRAL o SOBRECOMPRA.</param>
/// <param name="MACD">Valor de la línea MACD.</param>
/// <param name="ConfirmacionAlcista">Indica si el MACD confirma una tendencia alcista.</param>
/// <param name="BollingerSuperior">Valor de la banda superior de Bollinger (0 si no hay datos suficientes).</param>
/// <param name="BollingerInferior">Valor de la banda inferior de Bollinger (0 si no hay datos suficientes).</param>
/// <param name="CercaBandaInferior">Verdadero si el precio está dentro del 2% de la banda inferior (señal alcista).</param>
/// <param name="CercaBandaSuperior">Verdadero si el precio está dentro del 2% de la banda superior (señal bajista).</param>
/// <param name="EMA20">Valor de la media móvil exponencial de 20 períodos (0 si no hay datos suficientes).</param>
/// <param name="TendenciaAlcista">Verdadero si el precio actual está por encima de la EMA 20.</param>
public record SeñalesTecnicas(
    double RSI,
    string ClasificacionRSI,
    double MACD,
    bool ConfirmacionAlcista,
    double BollingerSuperior,
    double BollingerInferior,
    bool CercaBandaInferior,
    bool CercaBandaSuperior,
    double EMA20,
    bool TendenciaAlcista);
