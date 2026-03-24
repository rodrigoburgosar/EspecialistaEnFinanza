using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Contratos.Interfaces;

namespace StockAnalyzer.Agentes;

/// <summary>
/// Agente responsable de obtener titulares de noticias financieras relevantes
/// para un ticker desde feeds RSS (Yahoo Finance) y NewsAPI.
/// Si una fuente falla, continúa con las demás sin interrumpir el análisis.
/// </summary>
public sealed class AgenteNoticias(
    IHttpClientFactory fabricaHttp,
    IConfiguration configuracion,
    ILogger<AgenteNoticias> logger) : IAgenteNoticias
{
    private const int MaximoTitulares = 20;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ObtenerTitularesAsync(
        string ticker,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Obteniendo noticias para {Ticker}", ticker);

        var tareas = new List<Task<IEnumerable<string>>>
        {
            ObtenerDesdeRssAsync(ticker, cancellationToken),
            ObtenerDesdeNewsApiAsync(ticker, cancellationToken)
        };

        var resultados = await Task.WhenAll(tareas);

        var corteHace24Horas = DateTime.UtcNow.AddHours(-24);

        var titulares = resultados
            .SelectMany(r => r)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximoTitulares)
            .ToList();

        logger.LogInformation("Se obtuvieron {Total} titulares para {Ticker}", titulares.Count, ticker);
        return titulares;
    }

    /// <summary>
    /// Obtiene titulares desde el feed RSS de Yahoo Finance para el ticker dado.
    /// </summary>
    private async Task<IEnumerable<string>> ObtenerDesdeRssAsync(string ticker, CancellationToken cancellationToken)
    {
        var url = $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={ticker}&region=US&lang=en-US";
        try
        {
            var cliente = fabricaHttp.CreateClient();
            var contenido = await cliente.GetStringAsync(url, cancellationToken);

            using var lector = XmlReader.Create(new StringReader(contenido));
            var feed = SyndicationFeed.Load(lector);

            var hace24Horas = DateTime.UtcNow.AddHours(-24);

            return feed.Items
                .Where(item => item.PublishDate.UtcDateTime >= hace24Horas)
                .Select(item => item.Title.Text)
                .Where(titulo => EsRelevante(titulo, ticker));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error al obtener RSS de Yahoo Finance para {Ticker}. Continuando con otras fuentes.", ticker);
            return [];
        }
    }

    /// <summary>
    /// Obtiene titulares desde Newsdata.io filtrando por el ticker o empresa.
    /// </summary>
    private async Task<IEnumerable<string>> ObtenerDesdeNewsApiAsync(string ticker, CancellationToken cancellationToken)
    {
        var apiKey = configuracion["NewsApi:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("NewsApi:ApiKey no configurado. Se omite Newsdata.io.");
            return [];
        }

        var url = $"https://newsdata.io/api/1/latest?apikey={apiKey}&q={ticker}&language=en";
        try
        {
            var cliente = fabricaHttp.CreateClient();
            var respuesta = await cliente.GetStringAsync(url, cancellationToken);

            using var documento = JsonDocument.Parse(respuesta);
            var resultados = documento.RootElement.GetProperty("results");

            var hace24Horas = DateTime.UtcNow.AddHours(-24);

            return resultados.EnumerateArray()
                .Where(a =>
                {
                    var fechaStr = a.GetProperty("pubDate").GetString();
                    return DateTime.TryParse(fechaStr, out var fecha) && fecha.ToUniversalTime() >= hace24Horas;
                })
                .Select(a => a.GetProperty("title").GetString() ?? string.Empty)
                .Where(titulo => !string.IsNullOrWhiteSpace(titulo) && EsRelevante(titulo, ticker))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error al obtener noticias de Newsdata.io para {Ticker}. Continuando sin esta fuente.", ticker);
            return [];
        }
    }

    /// <summary>
    /// Determina si un titular es relevante para el ticker buscado.
    /// </summary>
    /// <param name="titular">Texto del titular a evaluar.</param>
    /// <param name="ticker">Símbolo bursátil (ej. "PLTR").</param>
    private static bool EsRelevante(string titular, string ticker) =>
        titular.Contains(ticker, StringComparison.OrdinalIgnoreCase) ||
        titular.Contains("Palantir", StringComparison.OrdinalIgnoreCase);
}
