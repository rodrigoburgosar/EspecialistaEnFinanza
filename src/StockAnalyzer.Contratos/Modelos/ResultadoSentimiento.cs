namespace StockAnalyzer.Contratos.Modelos;

/// <summary>
/// Resultado retornado por el microservicio Python de análisis de sentimiento (FinBERT).
/// </summary>
/// <param name="Puntaje">Score consolidado de sentimiento entre -1.0 (muy negativo) y +1.0 (muy positivo).</param>
/// <param name="TotalAnalizados">Cantidad de titulares que fueron procesados por el modelo.</param>
public record ResultadoSentimiento(double Puntaje, int TotalAnalizados);
