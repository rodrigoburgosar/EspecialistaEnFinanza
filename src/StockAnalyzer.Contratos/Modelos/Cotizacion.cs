namespace StockAnalyzer.Contratos.Modelos;

/// <summary>
/// Representa una cotización de precio OHLCV (apertura, máximo, mínimo, cierre, volumen)
/// para un día de mercado específico.
/// </summary>
/// <param name="Fecha">Fecha de la cotización.</param>
/// <param name="Apertura">Precio de apertura del día.</param>
/// <param name="Maximo">Precio máximo alcanzado en el día.</param>
/// <param name="Minimo">Precio mínimo alcanzado en el día.</param>
/// <param name="Cierre">Precio de cierre del día.</param>
/// <param name="Volumen">Volumen de acciones negociadas en el día.</param>
public record Cotizacion(
    DateOnly Fecha,
    decimal Apertura,
    decimal Maximo,
    decimal Minimo,
    decimal Cierre,
    long Volumen);
