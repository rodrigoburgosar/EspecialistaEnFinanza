using Microsoft.AspNetCore.Mvc.RazorPages;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Api.Pages.Dashboard;

/// <summary>
/// PageModel del dashboard web. Consulta el historial de recomendaciones
/// aplicando filtros opcionales por ticker y tipo de acción.
/// </summary>
public sealed class IndexModel(IRepositorioRecomendaciones repositorio) : PageModel
{
    /// <summary>Filtro de ticker seleccionado por el usuario (puede ser null para todos).</summary>
    public string? TickerFiltro { get; set; }

    /// <summary>Filtro de acción seleccionado por el usuario (puede ser null para todas).</summary>
    public string? AccionFiltro { get; set; }

    /// <summary>Historial de recomendaciones aplicando los filtros activos.</summary>
    public IEnumerable<Recomendacion> Historial { get; private set; } = [];

    /// <summary>Resumen por ticker: última recomendación y las últimas 5 señales.</summary>
    public IEnumerable<ResumenTicker> Resumen { get; private set; } = [];

    /// <summary>
    /// Carga el historial y el resumen estadístico al navegar a la página.
    /// </summary>
    /// <param name="ticker">Filtro por ticker desde la query string.</param>
    /// <param name="accion">Filtro por acción desde la query string.</param>
    public async Task OnGetAsync(string? ticker, string? accion)
    {
        TickerFiltro = ticker;
        AccionFiltro = accion;

        Historial = await repositorio.ObtenerHistorialAsync(ticker, accion, cantidad: 50);
        Resumen = await ConstruirResumenAsync();
    }

    /// <summary>
    /// Construye las tarjetas de resumen por ticker a partir del historial completo sin filtros.
    /// </summary>
    private async Task<IEnumerable<ResumenTicker>> ConstruirResumenAsync()
    {
        var todasLasRecomendaciones = await repositorio.ObtenerHistorialAsync(null, null, cantidad: 200);

        return todasLasRecomendaciones
            .GroupBy(r => r.Ticker)
            .Select(grupo =>
            {
                var ordenadas = grupo.OrderByDescending(r => r.Fecha).ToList();
                var ultima = ordenadas.First();
                var ultimas5 = ordenadas.Take(5).ToList();

                return new ResumenTicker(
                    Ticker: ultima.Ticker,
                    UltimaAccion: ultima.Accion,
                    UltimaConfianza: ultima.Confianza,
                    RSI: ultima.RSI,
                    Sentimiento: ultima.Sentimiento,
                    IconosTendencia: ultimas5.Select(r => r.Accion switch
                    {
                        "COMPRAR" => "🟢",
                        "VENDER" => "🔴",
                        _ => "⚪"
                    }).ToList());
            })
            .OrderBy(r => r.Ticker);
    }
}

/// <summary>
/// Datos de resumen por ticker para mostrar en las tarjetas del dashboard.
/// </summary>
/// <param name="Ticker">Símbolo bursátil.</param>
/// <param name="UltimaAccion">Última acción recomendada.</param>
/// <param name="UltimaConfianza">Nivel de confianza de la última recomendación.</param>
/// <param name="RSI">RSI en la última recomendación.</param>
/// <param name="Sentimiento">Score de sentimiento en la última recomendación.</param>
/// <param name="IconosTendencia">Lista de íconos (🟢/🔴/⚪) para las últimas 5 señales.</param>
public record ResumenTicker(
    string Ticker,
    string UltimaAccion,
    string UltimaConfianza,
    double RSI,
    double Sentimiento,
    IReadOnlyList<string> IconosTendencia);
