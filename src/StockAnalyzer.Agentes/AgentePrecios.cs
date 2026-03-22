using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Agentes;

/// <summary>
/// Agente responsable de obtener los precios históricos OHLCV de un ticker
/// desde Alpha Vantage API y normalizarlos al modelo interno <see cref="Cotizacion"/>.
/// Mantiene un caché en memoria con TTL de 4 horas por ticker para respetar
/// los límites de la API y evitar llamadas duplicadas dentro del mismo ciclo.
/// </summary>
public sealed class AgentePrecios(
    IHttpClientFactory fabricaHttp,
    IConfiguration configuracion,
    IMemoryCache cache,
    ILogger<AgentePrecios> logger) : IAgentePrecios
{
    private const string UrlBaseAlphaVantage = "https://www.alphavantage.co/query";
    private static readonly TimeSpan TtlCache = TimeSpan.FromHours(4);

    /// <inheritdoc/>
    public async Task<IEnumerable<Cotizacion>> ObtenerPreciosAsync(
        string ticker,
        int dias = 25,
        CancellationToken cancellationToken = default)
    {
        var claveCache = $"precios:{ticker.ToUpperInvariant()}:{dias}";

        if (cache.TryGetValue(claveCache, out IEnumerable<Cotizacion>? cotizacionesCacheadas)
            && cotizacionesCacheadas is not null)
        {
            logger.LogInformation("Precios de {Ticker} obtenidos desde caché (TTL 4h)", ticker);
            return cotizacionesCacheadas;
        }

        var apiKey = configuracion["AlphaVantage:ApiKey"]
            ?? throw new InvalidOperationException("ALPHA_VANTAGE_KEY no está configurado.");

        var url = $"{UrlBaseAlphaVantage}?function=TIME_SERIES_DAILY&symbol={ticker}&apikey={apiKey}&outputsize=compact";

        logger.LogInformation("Obteniendo precios de {Ticker} desde Alpha Vantage (ventana: {Dias} días)", ticker, dias);

        var cliente = fabricaHttp.CreateClient("AlphaVantage");

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await cliente.GetAsync(url, cancellationToken);
            respuesta.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al obtener precios de {Ticker} desde Alpha Vantage", ticker);
            throw new InvalidOperationException($"No se pudieron obtener los precios de {ticker}.", ex);
        }

        var json = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        var cotizaciones = ParsearCotizaciones(json, ticker, dias).ToList();

        cache.Set(claveCache, (IEnumerable<Cotizacion>)cotizaciones, TtlCache);
        logger.LogInformation("Precios de {Ticker} almacenados en caché por {Horas}h", ticker, TtlCache.TotalHours);

        return cotizaciones;
    }

    /// <summary>
    /// Parsea la respuesta JSON de Alpha Vantage y retorna las últimas N cotizaciones.
    /// </summary>
    /// <param name="json">Respuesta JSON de la API.</param>
    /// <param name="ticker">Símbolo del ticker para logging.</param>
    /// <param name="dias">Cantidad de días a retornar.</param>
    /// <returns>Colección de cotizaciones ordenadas por fecha ascendente.</returns>
    private IEnumerable<Cotizacion> ParsearCotizaciones(string json, string ticker, int dias)
    {
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        if (!raiz.TryGetProperty("Time Series (Daily)", out var serieTemporal))
        {
            logger.LogWarning("Respuesta de Alpha Vantage para {Ticker} no contiene datos de precio", ticker);
            return [];
        }

        var cotizaciones = new List<Cotizacion>();

        foreach (var entrada in serieTemporal.EnumerateObject())
        {
            if (!DateOnly.TryParse(entrada.Name, out var fecha))
                continue;

            var datos = entrada.Value;
            cotizaciones.Add(new Cotizacion(
                Fecha: fecha,
                Apertura: decimal.Parse(datos.GetProperty("1. open").GetString() ?? "0", CultureInfo.InvariantCulture),
                Maximo: decimal.Parse(datos.GetProperty("2. high").GetString() ?? "0", CultureInfo.InvariantCulture),
                Minimo: decimal.Parse(datos.GetProperty("3. low").GetString() ?? "0", CultureInfo.InvariantCulture),
                Cierre: decimal.Parse(datos.GetProperty("4. close").GetString() ?? "0", CultureInfo.InvariantCulture),
                Volumen: long.Parse(datos.GetProperty("5. volume").GetString() ?? "0", CultureInfo.InvariantCulture)));
        }

        return cotizaciones
            .OrderBy(c => c.Fecha)
            .TakeLast(dias);
    }
}
