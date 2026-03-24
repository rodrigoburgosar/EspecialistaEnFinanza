namespace StockAnalyzer.Contratos.Modelos;

/// <summary>
/// Resultado final del análisis: la recomendación de acción sobre un ticker,
/// incluyendo las métricas que fundamentan la decisión.
/// </summary>
/// <param name="Ticker">Símbolo bursátil analizado (ej. "PLTR").</param>
/// <param name="Accion">Acción recomendada: COMPRAR, VENDER o MANTENER.</param>
/// <param name="Confianza">Nivel de confianza de la recomendación: ALTA o MEDIA.</param>
/// <param name="RSI">Valor del RSI en el momento del análisis.</param>
/// <param name="Sentimiento">Score de sentimiento de noticias entre -1.0 y +1.0.</param>
/// <param name="NoticiasAnalizadas">Cantidad de titulares procesados por el análisis de sentimiento.</param>
/// <param name="Fecha">Fecha y hora UTC en que se generó la recomendación.</param>
public record Recomendacion(
    string Ticker,
    string Accion,
    string Confianza,
    double RSI,
    double Sentimiento,
    int NoticiasAnalizadas,
    DateTime Fecha);
