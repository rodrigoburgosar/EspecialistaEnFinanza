using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Api.Datos;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Tests.Datos;

/// <summary>
/// Pruebas unitarias para <see cref="RepositorioRecomendaciones"/>.
/// Usa EF Core InMemory para evitar dependencia de base de datos real.
/// </summary>
public class RepositorioRecomendacionesTests : IDisposable
{
    private readonly ContextoBd _contexto;
    private readonly RepositorioRecomendaciones _repositorio;

    public RepositorioRecomendacionesTests()
    {
        var opciones = new DbContextOptionsBuilder<ContextoBd>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _contexto = new ContextoBd(opciones);
        _repositorio = new RepositorioRecomendaciones(_contexto);
    }

    public void Dispose() => _contexto.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Recomendacion CrearRecomendacion(
        string ticker = "PLTR",
        string accion = "COMPRAR",
        string confianza = "ALTA",
        double rsi = 25.0,
        double sentimiento = 0.6,
        DateTime? fecha = null) =>
        new(ticker, accion, confianza, rsi, sentimiento, 10, fecha ?? DateTime.UtcNow);

    // ── GuardarAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GuardarAsync_NuevaRecomendacion_PersisteEnBd()
    {
        var recomendacion = CrearRecomendacion();

        await _repositorio.GuardarAsync(recomendacion);

        _contexto.Recomendaciones.Should().HaveCount(1);
        _contexto.Recomendaciones.First().Ticker.Should().Be("PLTR");
    }

    // ── ObtenerUltimaAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerUltimaAsync_ConRecomendaciones_RetornaLaMasReciente()
    {
        var antigua = CrearRecomendacion(fecha: DateTime.UtcNow.AddHours(-8));
        var reciente = CrearRecomendacion(accion: "VENDER", fecha: DateTime.UtcNow.AddHours(-1));

        await _repositorio.GuardarAsync(antigua);
        await _repositorio.GuardarAsync(reciente);

        var resultado = await _repositorio.ObtenerUltimaAsync("PLTR");

        resultado.Should().NotBeNull();
        resultado!.Accion.Should().Be("VENDER");
    }

    [Fact]
    public async Task ObtenerUltimaAsync_TickerInexistente_RetornaNull()
    {
        var resultado = await _repositorio.ObtenerUltimaAsync("AAAA");

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerUltimaAsync_FiltradoPorTicker_NoMezclaOtrosTickers()
    {
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", "COMPRAR"));
        await _repositorio.GuardarAsync(CrearRecomendacion("MSFT", "VENDER"));

        var resultado = await _repositorio.ObtenerUltimaAsync("MSFT");

        resultado!.Accion.Should().Be("VENDER");
        resultado.Ticker.Should().Be("MSFT");
    }

    // ── ObtenerHistorialAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerHistorialAsync_SinFiltros_RetornaTodasOrdenadaPorFechaDesc()
    {
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", fecha: DateTime.UtcNow.AddHours(-3)));
        await _repositorio.GuardarAsync(CrearRecomendacion("MSFT", fecha: DateTime.UtcNow.AddHours(-1)));
        await _repositorio.GuardarAsync(CrearRecomendacion("NVDA", fecha: DateTime.UtcNow.AddHours(-2)));

        var historial = (await _repositorio.ObtenerHistorialAsync(null, null)).ToList();

        historial.Should().HaveCount(3);
        historial[0].Ticker.Should().Be("MSFT"); // la más reciente primero
        historial[2].Ticker.Should().Be("PLTR"); // la más antigua al final
    }

    [Fact]
    public async Task ObtenerHistorialAsync_FiltradoPorTicker_RetornaSoloDicho()
    {
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR"));
        await _repositorio.GuardarAsync(CrearRecomendacion("MSFT"));
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", "VENDER"));

        var historial = (await _repositorio.ObtenerHistorialAsync("PLTR", null)).ToList();

        historial.Should().HaveCount(2);
        historial.Should().AllSatisfy(r => r.Ticker.Should().Be("PLTR"));
    }

    [Fact]
    public async Task ObtenerHistorialAsync_FiltradoPorAccion_RetornaSoloDicha()
    {
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", "COMPRAR"));
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", "VENDER"));
        await _repositorio.GuardarAsync(CrearRecomendacion("MSFT", "COMPRAR"));

        var historial = (await _repositorio.ObtenerHistorialAsync(null, "COMPRAR")).ToList();

        historial.Should().HaveCount(2);
        historial.Should().AllSatisfy(r => r.Accion.Should().Be("COMPRAR"));
    }

    [Fact]
    public async Task ObtenerHistorialAsync_FiltroTickerYAccion_CombinaDosCondiciones()
    {
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", "COMPRAR"));
        await _repositorio.GuardarAsync(CrearRecomendacion("PLTR", "VENDER"));
        await _repositorio.GuardarAsync(CrearRecomendacion("MSFT", "COMPRAR"));

        var historial = (await _repositorio.ObtenerHistorialAsync("PLTR", "COMPRAR")).ToList();

        historial.Should().HaveCount(1);
        historial[0].Ticker.Should().Be("PLTR");
        historial[0].Accion.Should().Be("COMPRAR");
    }

    [Fact]
    public async Task ObtenerHistorialAsync_LimiteCantidad_RespetaElMaximo()
    {
        for (int i = 0; i < 10; i++)
            await _repositorio.GuardarAsync(CrearRecomendacion());

        var historial = (await _repositorio.ObtenerHistorialAsync(null, null, cantidad: 3)).ToList();

        historial.Should().HaveCount(3);
    }

    [Fact]
    public async Task ObtenerHistorialAsync_SinRecomendaciones_RetornaColeccionVacia()
    {
        var historial = await _repositorio.ObtenerHistorialAsync(null, null);

        historial.Should().BeEmpty();
    }
}
