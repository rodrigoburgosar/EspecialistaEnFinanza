---
name: generar-tests
description: Genera pruebas unitarias para una clase C# del proyecto, siguiendo las convenciones del proyecto (xUnit + FluentAssertions + NSubstitute). Úsalo cuando el usuario pida escribir tests para un agente, repositorio u otra clase.
license: MIT
metadata:
  author: proyecto
  version: "1.0"
---

Genera pruebas unitarias completas para una clase C# de este proyecto.

## Pasos

### 1. Identificar el objetivo

Si el usuario no especificó qué clase testear, usar **AskUserQuestion** para preguntar:
> "¿Para qué clase quieres que genere las pruebas? (ej. AgenteNoticias, Orquestador, RecomendacionController)"

### 2. Leer la clase objetivo

Localizar el archivo con **Glob** y leerlo con **Read**.
- Agentes: `src/StockAnalyzer.Agentes/<Clase>.cs`
- Repositorios: `src/StockAnalyzer.Api/Datos/<Clase>.cs`
- Controllers: `src/StockAnalyzer.Api/Controllers/<Clase>.cs`
- Orquestador: `src/StockAnalyzer.Orquestador/Orquestador.cs`

### 3. Leer la interfaz correspondiente

Si la clase implementa una interfaz en `StockAnalyzer.Contratos`, leerla también para entender el contrato completo.

### 4. Leer un test existente como referencia

Leer **uno** de los archivos de test ya existentes para mantener consistencia de estilo:
- `tests/StockAnalyzer.Tests/Agentes/AgenteDecisionTests.cs` — referencia para agentes con lógica pura
- `tests/StockAnalyzer.Tests/Agentes/AgentePreciosTests.cs` — referencia para agentes con HTTP y caché
- `tests/StockAnalyzer.Tests/Datos/RepositorioRecomendacionesTests.cs` — referencia para repositorios EF Core

### 5. Verificar si ya existe un archivo de tests

Con **Glob**: `tests/StockAnalyzer.Tests/**/<Clase>Tests.cs`
- Si existe → leerlo y **agregar** tests nuevos al final, sin duplicar los existentes
- Si no existe → crear el archivo desde cero

### 6. Identificar los casos a cubrir

Para cada método público de la clase, identificar:

| Tipo de caso | Descripción |
|---|---|
| **Camino feliz** | Input válido → output correcto |
| **Límites de umbrales** | Valores exactamente en el borde (ej. RSI = 30.0 vs 29.9) |
| **Datos insuficientes** | Colecciones vacías, nulls, menos datos de los requeridos |
| **Errores esperados** | Excepciones correctas con mensajes útiles |
| **Caché / efecto lateral** | Verificar que no se hacen llamadas duplicadas, que se persiste, etc. |

### 7. Escribir los tests

**Reglas obligatorias del proyecto:**

```csharp
// Nombre: Método_Escenario_ResultadoEsperado (en español)
public void Evaluar_RsiSobreventa_SentimientoPositivo_RetornaComprar()

// ILogger → siempre NullLogger, nunca mock
var agente = new MiAgente(NullLogger<MiAgente>.Instance);

// IMemoryCache → instancia real, no mock
var cache = new MemoryCache(new MemoryCacheOptions());

// EF Core → InMemory con GUID único por test
var opciones = new DbContextOptionsBuilder<ContextoBd>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;

// decimal.Parse → SIEMPRE con InvariantCulture
decimal.Parse("22.80", CultureInfo.InvariantCulture)

// HTTP falso → ManejadorHttpFalso (ya existe en AgentePreciosTests.cs)
// Si la clase bajo test necesita HttpClient, reusar esa clase interna

// Assertions con FluentAssertions
resultado.Accion.Should().Be("COMPRAR");
resultado.Should().NotBeNull();
act.Should().ThrowAsync<InvalidOperationException>();

// Mocks con NSubstitute
var repo = Substitute.For<IRepositorioRecomendaciones>();
repo.ObtenerUltimaAsync("PLTR", default).Returns(miRecomendacion);
repo.Received(1).GuardarAsync(Arg.Any<Recomendacion>(), Arg.Any<CancellationToken>());
```

**Estructura del archivo:**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StockAnalyzer.Agentes;           // ajustar según namespace
using StockAnalyzer.Contratos.Modelos;

namespace StockAnalyzer.Tests.<Carpeta>;

/// <summary>
/// Pruebas unitarias para <see cref="<Clase>"/>.
/// Descripción breve de qué se verifica.
/// </summary>
public class <Clase>Tests
{
    // ── campos ───────────────────────────────────────────────────────────────
    // instanciar la clase bajo test y sus dependencias aquí

    // ── helpers ──────────────────────────────────────────────────────────────
    // métodos privados que crean datos de prueba reutilizables

    // ── <Método1> ─────────────────────────────────────────────────────────────
    [Fact] ...

    // ── <Método2> ─────────────────────────────────────────────────────────────
    [Fact] ...
}
```

**Cantidad mínima de tests por clase:**
- Agentes con lógica pura (AgenteDecision): ≥ 6 tests
- Agentes con I/O (AgentePrecios, AgenteNoticias): ≥ 5 tests
- Repositorios: ≥ 1 test por método público

### 8. Guardar el archivo

Guardar en `tests/StockAnalyzer.Tests/<Carpeta>/<Clase>Tests.cs`.

La carpeta sigue la misma clasificación que la fuente:
- `Agentes/` para clases de `StockAnalyzer.Agentes`
- `Datos/` para clases de `StockAnalyzer.Api/Datos`
- `Controllers/` para clases de `StockAnalyzer.Api/Controllers`
- `Orquestador/` para `Orquestador.cs`

### 9. Compilar y ejecutar

```bash
dotnet build tests/StockAnalyzer.Tests
dotnet test tests/StockAnalyzer.Tests --filter "FullyQualifiedName~<Clase>Tests"
```

Si hay errores de compilación, corregirlos antes de terminar.
Si algún test falla, analizar la causa: ¿el test es incorrecto o reveló un bug real?

### 10. Reportar resultados

Mostrar al usuario:
- Nombre del archivo creado/modificado
- Cantidad de tests agregados y su clasificación
- Resultado de `dotnet test` (N passed / N failed)
- Si se encontró algún bug durante la escritura de tests, mencionarlo explícitamente

## Qué NO hacer

- No generar tests que dependan de red real, API keys o PostgreSQL
- No mockear `IMemoryCache` — usar la implementación real
- No mockear `ILogger` — usar `NullLogger<T>.Instance`
- No omitir el paso de compilar y ejecutar antes de reportar
- No crear tests triviales que solo verifican que el constructor no lanza excepción
- No duplicar tests que ya existen en el archivo
