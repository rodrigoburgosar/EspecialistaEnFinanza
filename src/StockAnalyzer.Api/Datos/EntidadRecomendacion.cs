namespace StockAnalyzer.Api.Datos;

/// <summary>
/// Entidad de base de datos que representa una recomendación persistida
/// en la tabla Recomendaciones de SQLite.
/// </summary>
public class EntidadRecomendacion
{
    /// <summary>Identificador único autoincremental.</summary>
    public int Id { get; set; }

    /// <summary>Símbolo bursátil analizado (ej. "PLTR").</summary>
    public required string Ticker { get; set; }

    /// <summary>Acción recomendada: COMPRAR, VENDER o MANTENER.</summary>
    public required string Accion { get; set; }

    /// <summary>Nivel de confianza: ALTA o MEDIA.</summary>
    public required string Confianza { get; set; }

    /// <summary>Valor del RSI en el momento del análisis.</summary>
    public double RSI { get; set; }

    /// <summary>Score de sentimiento entre -1.0 y +1.0.</summary>
    public double Sentimiento { get; set; }

    /// <summary>Cantidad de noticias analizadas.</summary>
    public int NoticiasAnalizadas { get; set; }

    /// <summary>Fecha y hora UTC de generación de la recomendación.</summary>
    public DateTime Fecha { get; set; }
}
