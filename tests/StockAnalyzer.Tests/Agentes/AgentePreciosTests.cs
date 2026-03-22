using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StockAnalyzer.Agentes;

namespace StockAnalyzer.Tests.Agentes;

/// <summary>
/// Pruebas unitarias para <see cref="AgentePrecios"/>.
/// Verifica el parseo de respuestas de Alpha Vantage y el comportamiento del caché.
/// </summary>
public class AgentePreciosTests
{
    private const string ApiKeyFalsa = "TEST_KEY";

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration CrearConfiguracion() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AlphaVantage:ApiKey"] = ApiKeyFalsa
            })
            .Build();

    private static IMemoryCache CrearCacheVacio() => new MemoryCache(new MemoryCacheOptions());

    private static IHttpClientFactory CrearFabricaHttp(string contenidoJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var respuesta = new HttpResponseMessage(status)
        {
            Content = new StringContent(contenidoJson, Encoding.UTF8, "application/json")
        };

        var manejador = new ManejadorHttpFalso(respuesta);
        var cliente = new HttpClient(manejador);

        var fabrica = Substitute.For<IHttpClientFactory>();
        fabrica.CreateClient(Arg.Any<string>()).Returns(cliente);
        return fabrica;
    }

    private static string JsonAlphaVantageValido(string ticker = "PLTR") => $$"""
        {
          "Meta Data": {
            "2. Symbol": "{{ticker}}"
          },
          "Time Series (Daily)": {
            "2024-03-20": {
              "1. open": "22.50",
              "2. high": "23.00",
              "3. low": "22.00",
              "4. close": "22.80",
              "5. volume": "30000000"
            },
            "2024-03-19": {
              "1. open": "21.00",
              "2. high": "22.10",
              "3. low": "20.80",
              "4. close": "21.90",
              "5. volume": "25000000"
            },
            "2024-03-18": {
              "1. open": "20.00",
              "2. high": "21.50",
              "3. low": "19.90",
              "4. close": "21.00",
              "5. volume": "28000000"
            }
          }
        }
        """;

    // ── parseo ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerPreciosAsync_RespuestaValida_RetornaCotizacionesOrdenadas()
    {
        var agente = new AgentePrecios(
            CrearFabricaHttp(JsonAlphaVantageValido()),
            CrearConfiguracion(),
            CrearCacheVacio(),
            NullLogger<AgentePrecios>.Instance);

        var cotizaciones = (await agente.ObtenerPreciosAsync("PLTR", dias: 25)).ToList();

        cotizaciones.Should().HaveCount(3);
        cotizaciones.Should().BeInAscendingOrder(c => c.Fecha);
        cotizaciones.Last().Cierre.Should().Be(22.80m);
    }

    [Fact]
    public async Task ObtenerPreciosAsync_RespuestaSinTimeSeries_RetornaColeccionVacia()
    {
        var jsonSinDatos = """{ "Note": "Thank you for using Alpha Vantage!" }""";
        var agente = new AgentePrecios(
            CrearFabricaHttp(jsonSinDatos),
            CrearConfiguracion(),
            CrearCacheVacio(),
            NullLogger<AgentePrecios>.Instance);

        var cotizaciones = await agente.ObtenerPreciosAsync("PLTR");

        cotizaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerPreciosAsync_RespuestaHttp500_LanzaInvalidOperationException()
    {
        var agente = new AgentePrecios(
            CrearFabricaHttp("{}", HttpStatusCode.InternalServerError),
            CrearConfiguracion(),
            CrearCacheVacio(),
            NullLogger<AgentePrecios>.Instance);

        var act = async () => await agente.ObtenerPreciosAsync("PLTR");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PLTR*");
    }

    // ── caché ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerPreciosAsync_SegundaLlamadaMismoTicker_UsaCacheNoLlamaApi()
    {
        var fabrica = CrearFabricaHttp(JsonAlphaVantageValido());
        var agente = new AgentePrecios(
            fabrica,
            CrearConfiguracion(),
            CrearCacheVacio(),
            NullLogger<AgentePrecios>.Instance);

        await agente.ObtenerPreciosAsync("PLTR", dias: 25);
        await agente.ObtenerPreciosAsync("PLTR", dias: 25);

        // El cliente HTTP solo debe haberse creado una vez (primera llamada)
        fabrica.Received(1).CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task ObtenerPreciosAsync_TickersDiferentes_HaceDosLlamadasApi()
    {
        var fabrica = CrearFabricaHttp(JsonAlphaVantageValido());
        var agente = new AgentePrecios(
            fabrica,
            CrearConfiguracion(),
            CrearCacheVacio(),
            NullLogger<AgentePrecios>.Instance);

        await agente.ObtenerPreciosAsync("PLTR", dias: 25);
        await agente.ObtenerPreciosAsync("MSFT", dias: 25);

        fabrica.Received(2).CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task ObtenerPreciosAsync_SinApiKeyConfigurada_LanzaInvalidOperationException()
    {
        var configSinKey = new ConfigurationBuilder().Build();
        var agente = new AgentePrecios(
            CrearFabricaHttp("{}"),
            configSinKey,
            CrearCacheVacio(),
            NullLogger<AgentePrecios>.Instance);

        var act = async () => await agente.ObtenerPreciosAsync("PLTR");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ALPHA_VANTAGE_KEY*");
    }
}

/// <summary>
/// Manejador HTTP falso que retorna siempre la misma respuesta configurada,
/// sin hacer llamadas de red reales.
/// </summary>
internal sealed class ManejadorHttpFalso(HttpResponseMessage respuesta) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(respuesta);
}
