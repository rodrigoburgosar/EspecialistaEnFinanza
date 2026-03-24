namespace StockAnalyzer.Contratos.Interfaces;

/// <summary>
/// Contrato del agente responsable de obtener titulares de noticias
/// financieras relevantes para un ticker desde RSS y APIs externas.
/// </summary>
public interface IAgenteNoticias
{
    /// <summary>
    /// Obtiene los titulares de noticias relacionados con el ticker en las últimas 24 horas.
    /// </summary>
    /// <param name="ticker">Símbolo bursátil del activo (ej. "PLTR").</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de hasta 20 titulares relevantes ordenados por fecha descendente.</returns>
    Task<IReadOnlyList<string>> ObtenerTitularesAsync(
        string ticker,
        CancellationToken cancellationToken = default);
}
