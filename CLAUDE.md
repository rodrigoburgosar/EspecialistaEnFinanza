# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Idioma

Toda la comunicación, comentarios en código y documentación deben estar en **español**.

## Proyecto

Sistema multi-agente de análisis y recomendación de acciones bursátiles. Ver `PROPUESTA_SISTEMA_ANALISIS_ACCIONES.md` para la arquitectura completa. La propuesta de implementación está en `openspec/changes/stock-analysis-multi-agent/`.

## Comandos

```bash
# Compilar solución
dotnet build

# Ejecutar API
dotnet run --project src/StockAnalyzer.Api

# Ejecutar microservicio Python (NLP)
cd src/StockAnalyzer.NLP && uvicorn main:app --reload

# Ejecutar TODOS los tests
dotnet test

# Ejecutar solo el proyecto de tests unitarios
dotnet test tests/StockAnalyzer.Tests

# Ejecutar un test específico por nombre (parcial)
dotnet test tests/StockAnalyzer.Tests --filter "NombreDelTest"

# Ejecutar tests de una clase específica
dotnet test tests/StockAnalyzer.Tests --filter "FullyQualifiedName~AgenteDecisionTests"

# Ejecutar tests con salida detallada
dotnet test tests/StockAnalyzer.Tests -v normal
```

## Convenciones C#

### Nombres en español

Todas las clases, interfaces, métodos, propiedades y variables deben nombrarse en **español**.

```csharp
// ✅ Correcto
public class AgentePrecios { }
public interface IAgenteSeñalesTecnicas { }
public async Task<Recomendacion> ObtenerRecomendacionAsync(string ticker) { }
public double PuntajeConfianza { get; set; }

// ❌ Incorrecto
public class PriceAgent { }
public interface ITechnicalSignalAgent { }
public async Task<Recommendation> GetRecommendationAsync(string ticker) { }
```

Excepción: nombres técnicos sin traducción natural (RSI, MACD, HTTP, API, DTO) se mantienen en inglés como siglas.

### Documentación XML obligatoria

Toda clase pública y todo método público o protegido deben tener documentación XML en español.

**Clases:**
```csharp
/// <summary>
/// Agente responsable de obtener los precios históricos OHLCV de un ticker
/// desde proveedores externos como Alpha Vantage o Yahoo Finance.
/// </summary>
public class AgentePrecios : IAgentePrecios
```

**Métodos:**
```csharp
/// <summary>
/// Obtiene los precios históricos del ticker para los últimos N días.
/// </summary>
/// <param name="ticker">Símbolo bursátil del activo (ej. "PLTR").</param>
/// <param name="dias">Cantidad de días hacia atrás a consultar. Por defecto 14.</param>
/// <returns>Colección de cotizaciones ordenadas por fecha ascendente.</returns>
/// <exception cref="ExcepcionProveedorDatos">Si la API externa no responde o retorna error.</exception>
public async Task<IEnumerable<Cotizacion>> ObtenerPreciosAsync(string ticker, int dias = 14)
```

### Buenas prácticas C#

**Async/Await:** todos los métodos de I/O deben ser async hasta el Orchestrator.
```csharp
// ✅ Correcto — propagar async hasta arriba
public async Task<Recomendacion> AnalizarAsync(string ticker, CancellationToken ct = default)

// ❌ Incorrecto — bloquear el hilo
public Recomendacion Analizar(string ticker) => AnalizarAsync(ticker).Result;
```

**CancellationToken:** todos los métodos async que llaman a I/O externo deben aceptar `CancellationToken`.

**Inyección de dependencias:** usar constructor injection. No usar `new` para servicios.
```csharp
public class Orquestador
{
    private readonly IAgentePrecios _agentePrecios;
    private readonly IAgenteSeñalesTecnicas _agenteSeñales;

    public Orquestador(IAgentePrecios agentePrecios, IAgenteSeñalesTecnicas agenteSeñales)
    {
        _agentePrecios = agentePrecios;
        _agenteSeñales = agenteSeñales;
    }
}
```

**Records para modelos inmutables:**
```csharp
// Modelos de datos como records
public record Cotizacion(DateOnly Fecha, decimal Apertura, decimal Maximo, decimal Minimo, decimal Cierre, long Volumen);
public record Recomendacion(string Ticker, string Accion, string Confianza, double RSI, double Sentimiento, DateTime Fecha);
```

**Manejo de errores:** capturar excepciones en el Orchestrator, no en los agentes. Los agentes lanzan, el Orchestrator decide.

**Configuración:** nunca hardcodear API keys, tokens ni URLs. Siempre desde `IConfiguration` / `.env`.

```csharp
// ✅ Correcto
var token = _config["Telegram:BotToken"]
    ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN no configurado.");
```

**Logging:** usar `ILogger<T>` inyectado. No usar `Console.WriteLine` en producción.
```csharp
_logger.LogInformation("Análisis iniciado para {Ticker}", ticker);
_logger.LogError(ex, "Error al obtener precios de {Ticker}", ticker);
```

## Pruebas unitarias

### Proyecto y estructura

```
tests/StockAnalyzer.Tests/
├── Agentes/          → tests de AgentePrecios, AgenteDecision, AgenteSeñalesTecnicas
└── Datos/            → tests de RepositorioRecomendaciones (EF Core InMemory)
```

**Stack:** xUnit · FluentAssertions 6.x · NSubstitute 5.x · EF Core InMemory

### Convenciones de nombrado

El nombre de cada test sigue el patrón `Método_Escenario_ResultadoEsperado`:

```csharp
// ✅ Correcto
public void Evaluar_RsiSobreventa_SentimientoPositivo_BollingerInferior_RetornaComprarAlta()
public async Task ObtenerPreciosAsync_RespuestaHttp500_LanzaInvalidOperationException()

// ❌ Incorrecto
public void TestEvaluar1()
public void Comprar_Test()
```

### Reglas para escribir tests

- **Una sola aserción conceptual por test** — no mezclar escenarios distintos en un `[Fact]`
- **`[Theory] + [InlineData]`** para múltiples variaciones del mismo caso (ej. validación de umbrales)
- **`NullLogger<T>.Instance`** en lugar de mock para ILogger — evita ruido sin valor
- **`IMemoryCache` real** (`new MemoryCache(...)`) en lugar de mock — la lógica de caché se prueba con la implementación real
- **EF Core InMemory** con `Guid.NewGuid()` como nombre de BD — cada test recibe una BD limpia
- **`ManejadorHttpFalso`** (en `AgentePreciosTests.cs`) para simular respuestas HTTP sin red
- **`CultureInfo.InvariantCulture`** en cualquier `decimal.Parse` / `double.Parse` — el locale español usa coma como separador

### Agregar tests a una clase existente

Antes de escribir un test nuevo, leer el archivo de tests existente para la clase. Si no existe:
1. Crear `tests/StockAnalyzer.Tests/<Carpeta>/<Clase>Tests.cs`
2. Seguir la estructura de `AgenteDecisionTests.cs` como referencia
3. Ejecutar `dotnet test tests/StockAnalyzer.Tests` para verificar que pasan

Para generar tests automáticamente usa el skill `/generar-tests`.

## Convenciones Python (microservicio NLP)

- Nombres de funciones y variables en **español** con snake_case
- Docstrings en español para funciones públicas
- Type hints obligatorios

```python
def analizar_sentimiento(titulares: list[str]) -> float:
    """
    Analiza el sentimiento financiero de una lista de titulares usando FinBERT.

    Args:
        titulares: Lista de titulares de noticias financieras.

    Returns:
        Score consolidado entre -1.0 (negativo) y +1.0 (positivo).
    """
```

## Arquitectura de agentes

Cada agente tiene una única responsabilidad y se comunica solo a través del `Orquestador`:

```
Orquestador
├── AgentePrecios          → obtiene cotizaciones OHLCV
├── AgenteSeñalesTecnicas  → calcula RSI, MACD
├── AgenteNoticias         → consume RSS y NewsAPI
├── AgenteSentimiento      → llama al microservicio Python/FinBERT
├── AgenteDecision         → emite COMPRAR / VENDER / MANTENER
└── AgenteNotificacion     → envía alerta a Telegram (solo confianza ALTA)
```

Las interfaces viven en `StockAnalyzer.Contratos`. Ningún agente depende directamente de otro agente — solo de sus interfaces.
