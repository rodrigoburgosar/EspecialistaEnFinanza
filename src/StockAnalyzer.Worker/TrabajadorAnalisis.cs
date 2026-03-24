using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using OrquestadorServicio = StockAnalyzer.Orquestador.Orquestador;

namespace StockAnalyzer.Worker;

/// <summary>
/// Servicio en segundo plano que ejecuta el análisis de acciones periódicamente
/// cada 4 horas durante el horario de mercado (09:00–17:00 ET, lunes a viernes).
/// Itera sobre todos los tickers configurados en config/tickers.yaml.
/// </summary>
public sealed class TrabajadorAnalisis(
    OrquestadorServicio orquestador,
    IConfiguration configuracion,
    ILogger<TrabajadorAnalisis> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloAnalisis = TimeSpan.FromHours(4);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trabajador de análisis iniciado. Intervalo: {Intervalo}h", IntervaloAnalisis.TotalHours);

        using var temporizador = new PeriodicTimer(IntervaloAnalisis);

        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            if (!EsHorarioMercado())
            {
                logger.LogDebug("Fuera de horario de mercado. Esperando siguiente ciclo.");
                continue;
            }

            var tickers = ObtenerTickersConfigurados();

            foreach (var ticker in tickers)
            {
                try
                {
                    await orquestador.AnalizarAsync(ticker, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error durante el análisis de {Ticker}. Continuando con el siguiente.", ticker);
                }
            }
        }
    }

    /// <summary>
    /// Determina si el momento actual corresponde al horario de mercado
    /// (09:00–17:00 hora del Este, lunes a viernes).
    /// </summary>
    private bool EsHorarioMercado()
    {
        var zonaEste = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var ahoraEste = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaEste);

        var esDiaSemana = ahoraEste.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
        var esHorario = ahoraEste.TimeOfDay >= TimeSpan.FromHours(9) &&
                        ahoraEste.TimeOfDay <= TimeSpan.FromHours(17);

        return esDiaSemana && esHorario;
    }

    /// <summary>
    /// Lee la lista de tickers activos desde config/tickers.yaml.
    /// </summary>
    /// <returns>Lista de símbolos bursátiles a analizar.</returns>
    private IEnumerable<string> ObtenerTickersConfigurados()
    {
        var rutaConfig = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "tickers.yaml");

        if (!File.Exists(rutaConfig))
        {
            logger.LogWarning("Archivo tickers.yaml no encontrado en {Ruta}. Usando PLTR por defecto.", rutaConfig);
            return ["PLTR"];
        }

        try
        {
            var deserializador = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var contenido = File.ReadAllText(rutaConfig);
            var config = deserializador.Deserialize<ConfiguracionTickers>(contenido);

            return config.Tickers
                .Where(t => t.Activo)
                .Select(t => t.Simbolo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al leer tickers.yaml. Usando PLTR por defecto.");
            return ["PLTR"];
        }
    }

    // Clases auxiliares para deserializar tickers.yaml
    private sealed class ConfiguracionTickers
    {
        public List<TickerEntrada> Tickers { get; set; } = [];
    }

    private sealed class TickerEntrada
    {
        public string Simbolo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }
}
