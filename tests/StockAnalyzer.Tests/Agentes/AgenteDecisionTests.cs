using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockAnalyzer.Agentes;
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Tests.Agentes;

/// <summary>
/// Pruebas unitarias para <see cref="AgenteDecision"/>.
/// Verifica la tabla de decisión: RSI + sentimiento + confirmación Bollinger.
/// </summary>
public class AgenteDecisionTests
{
    private readonly AgenteDecision _agente = new(NullLogger<AgenteDecision>.Instance);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static SeñalesTecnicas Señales(
        double rsi,
        bool cercaBandaInferior = false,
        bool cercaBandaSuperior = false) =>
        new(RSI: rsi, ClasificacionRSI: "NEUTRAL",
            MACD: 0, ConfirmacionAlcista: false,
            BollingerSuperior: 0, BollingerInferior: 0,
            CercaBandaInferior: cercaBandaInferior,
            CercaBandaSuperior: cercaBandaSuperior,
            EMA20: 0, TendenciaAlcista: false);

    // ── COMPRAR ───────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluar_RsiSobreventa_SentimientoPositivo_BollingerInferior_RetornaComprarAlta()
    {
        var resultado = _agente.Evaluar("PLTR", Señales(25, cercaBandaInferior: true), 0.5, 10);

        resultado.Accion.Should().Be("COMPRAR");
        resultado.Confianza.Should().Be("ALTA");
    }

    [Fact]
    public void Evaluar_RsiSobreventa_SentimientoPositivo_SinBollinger_RetornaComprarMedia()
    {
        var resultado = _agente.Evaluar("PLTR", Señales(25, cercaBandaInferior: false), 0.5, 10);

        resultado.Accion.Should().Be("COMPRAR");
        resultado.Confianza.Should().Be("MEDIA");
    }

    [Fact]
    public void Evaluar_RsiEnLimite29_SentimientoJustoSobre0_3_RetornaComprar()
    {
        var resultado = _agente.Evaluar("PLTR", Señales(29.9), 0.31, 5);

        resultado.Accion.Should().Be("COMPRAR");
    }

    // ── VENDER ────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluar_RsiSobrecompra_SentimientoNegativo_BollingerSuperior_RetornaVenderAlta()
    {
        var resultado = _agente.Evaluar("PLTR", Señales(75, cercaBandaSuperior: true), -0.5, 10);

        resultado.Accion.Should().Be("VENDER");
        resultado.Confianza.Should().Be("ALTA");
    }

    [Fact]
    public void Evaluar_RsiSobrecompra_SentimientoNegativo_SinBollinger_RetornaVenderMedia()
    {
        var resultado = _agente.Evaluar("PLTR", Señales(75, cercaBandaSuperior: false), -0.5, 10);

        resultado.Accion.Should().Be("VENDER");
        resultado.Confianza.Should().Be("MEDIA");
    }

    // ── MANTENER ─────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluar_RsiNeutral_RetornaMantener()
    {
        var resultado = _agente.Evaluar("PLTR", Señales(50), 0.1, 5);

        resultado.Accion.Should().Be("MANTENER");
        resultado.Confianza.Should().Be("MEDIA");
    }

    [Fact]
    public void Evaluar_RsiSobreventa_SentimientoNegativo_RetornaMantener()
    {
        // Señales contradictorias: RSI bajo pero sentimiento negativo
        var resultado = _agente.Evaluar("PLTR", Señales(25), -0.6, 5);

        resultado.Accion.Should().Be("MANTENER");
    }

    [Fact]
    public void Evaluar_RsiSobrecompra_SentimientoPositivo_RetornaMantener()
    {
        // Señales contradictorias: RSI alto pero sentimiento positivo
        var resultado = _agente.Evaluar("PLTR", Señales(75), 0.7, 5);

        resultado.Accion.Should().Be("MANTENER");
    }

    [Fact]
    public void Evaluar_SentimientoExactoEnUmbral_RetornaMantener()
    {
        // Exactamente en el umbral 0.3 no lo supera (>0.3, no >=)
        var resultado = _agente.Evaluar("PLTR", Señales(25), 0.3, 5);

        resultado.Accion.Should().Be("MANTENER");
    }

    // ── Datos del resultado ───────────────────────────────────────────────────

    [Fact]
    public void Evaluar_ResultadoContieneTicker_Rsi_Sentimiento_Fecha()
    {
        var señales = Señales(25, cercaBandaInferior: true);
        var resultado = _agente.Evaluar("MSFT", señales, 0.6, 12);

        resultado.Ticker.Should().Be("MSFT");
        resultado.RSI.Should().Be(25);
        resultado.Sentimiento.Should().Be(0.6);
        resultado.NoticiasAnalizadas.Should().Be(12);
        resultado.Fecha.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
