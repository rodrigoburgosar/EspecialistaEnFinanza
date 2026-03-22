---
name: netcore8
description: >
  Advanced .NET 8 / ASP.NET Core 8 development skill following Microsoft's official guidelines and
  industry best practices. APIs are backoffice-oriented (internal, no public auth required by
  default). Use this skill whenever the user asks to create, review, refactor, or debug any .NET 8
  code, including: Web APIs, Minimal APIs, controllers, services, repositories, middleware, Entity
  Framework Core 8, background services, CQRS, clean architecture, dependency injection,
  configuration, logging, testing, performance optimization, or any C# 12 features. Trigger even
  for partial questions like "cómo hago X en .NET", "revisa mi controller", "crea un endpoint",
  "best practices en dotnet", or whenever someone pastes C# code and asks for help. Only load
  the security reference if the user explicitly asks about auth, JWT, or security.
---

# .NET 8 Advanced Skill

## Quick Reference — When to load which reference file

| Task | Reference file |
|------|---------------|
| Minimal APIs, endpoints, route groups | `references/minimal-apis.md` |
| Controllers, filtros, model binding | `references/controllers.md` |
| Entity Framework Core 8 | `references/efcore.md` |
| Inyección de dependencias, lifetimes | `references/dependency-injection.md` |
| CQRS, MediatR, Clean Architecture | `references/architecture.md` |
| Auth, JWT, OAuth2, policies *(solo si se pide explícitamente)* | `references/security.md` |
| Logging, OpenTelemetry, métricas | `references/observability.md` |
| Performance, caching, channels | `references/performance.md` |
| Testing unitario e integración | `references/testing.md` |
| C# 12 features, patrones modernos | `references/csharp12.md` |
| Middleware, pipeline, exception handler | `references/middleware.md` |
| Resilience, Polly, retry, circuit breaker | `references/resilience.md` |
| Messaging, MassTransit, Outbox pattern | `references/messaging.md` |
| Configuration, secrets, Options Pattern, Feature Flags | `references/configuration.md` |

**Regla:** Lee SOLO el/los archivos relevantes antes de responder. Para tareas multi-área, combina hasta 3 referencias.

## Contexto de las APIs — Backoffice

Estas APIs operan en **red interna / backoffice**. Esto implica:

- **No agregar autenticación por defecto** — no generar `[Authorize]`, JWT, ni políticas de auth a menos que el usuario lo pida explícitamente.
- **No agregar `UseAuthentication()` / `UseAuthorization()`** en Program.cs a menos que se solicite.
- Los endpoints son **accesibles directamente** sin token — diseñados para consumo interno entre servicios o desde herramientas de administración.
- El archivo `references/security.md` **solo se carga si el usuario pregunta explícitamente** sobre autenticación, autorización, JWT, OAuth, o seguridad.
- Si el usuario pide seguridad, consultar la referencia y aplicarla; si no, omitirla completamente del código generado.

---

## Principios Fundamentales

### 1. Seguir siempre las guías oficiales de Microsoft
- Usar los patrones recomendados en [docs.microsoft.com/aspnet/core](https://docs.microsoft.com/aspnet/core)
- Preferir las APIs nativas de .NET antes que librerías de terceros cuando exista equivalente
- Usar los analyzers de Roslyn — respetar CA*, SA*, IDE* warnings

### 2. C# 12 por defecto
Usar siempre las características modernas:
```csharp
// ✅ Primary constructors
public class UserService(IUserRepository repo, ILogger<UserService> logger) { }

// ✅ Collection expressions
int[] ids = [1, 2, 3];
List<string> names = [..existingNames, "nuevo"];

// ✅ Pattern matching avanzado
if (result is { IsSuccess: true, Value: var value })
    return value;

// ✅ Raw string literals
var json = """
    { "name": "test" }
    """;

// ✅ Required members
public class CreateUserDto
{
    public required string Email { get; init; }
    public required string Name { get; init; }
}
```

### 3. Inmutabilidad y Records
```csharp
// DTOs como records
public record CreateUserRequest(string Email, string Name, int Age);
public record UserResponse(Guid Id, string Email, string Name);

// Value objects
public record Email
{
    public string Value { get; }
    private Email(string value) => Value = value;
    public static Result<Email> Create(string email) =>
        IsValid(email) ? Result.Ok(new Email(email)) : Result.Fail("Email inválido");
}
```

### 4. Result Pattern — nunca lanzar excepciones para flujo de negocio
```csharp
// ❌ Mal — excepciones para control de flujo
public User GetUser(Guid id) => throw new NotFoundException("User not found");

// ✅ Bien — Result pattern
public async Task<Result<UserResponse>> GetUserAsync(Guid id, CancellationToken ct)
{
    var user = await _repo.GetByIdAsync(id, ct);
    return user is null
        ? Result.Fail<UserResponse>($"Usuario {id} no encontrado")
        : Result.Ok(user.ToResponse());
}
```

### 5. CancellationToken siempre en operaciones async
```csharp
// ✅ Toda operación async debe recibir y propagar CancellationToken
public async Task<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken = default)
{
    return await _context.Users
        .AsNoTracking()
        .ToListAsync(cancellationToken);
}
```

---

## Estructura de Proyecto Recomendada (Clean Architecture)

```
src/
├── MyApp.API/                    # Capa de presentación
│   ├── Endpoints/                # Minimal API endpoints (o Controllers/)
│   ├── Middleware/
│   ├── Filters/
│   └── Program.cs
├── MyApp.Application/            # Lógica de negocio
│   ├── Features/
│   │   └── Users/
│   │       ├── Commands/
│   │       │   └── CreateUser/
│   │       │       ├── CreateUserCommand.cs
│   │       │       ├── CreateUserHandler.cs
│   │       │       └── CreateUserValidator.cs
│   │       └── Queries/
│   │           └── GetUser/
│   ├── Common/
│   │   ├── Behaviors/           # Pipeline behaviors (logging, validation)
│   │   └── Interfaces/
│   └── DependencyInjection.cs
├── MyApp.Domain/                 # Entidades, Value Objects, eventos
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   └── Errors/
├── MyApp.Infrastructure/         # EF Core, repos, servicios externos
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/
│   │   └── Repositories/
│   ├── Services/
│   └── DependencyInjection.cs
tests/
├── MyApp.UnitTests/
├── MyApp.IntegrationTests/
└── MyApp.ArchitectureTests/
```

---

## Program.cs — Patrón Moderno

```csharp
var builder = WebApplication.CreateBuilder(args);

// Registrar capas via extension methods (no todo en Program.cs)
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Serilog estructurado
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await app.Services.MigrateDatabaseAsync(); // solo en dev/staging
}

app.UseExceptionHandler();  // .NET 8 built-in exception handler
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();
// ℹ️ APIs Backoffice: UseAuthentication/UseAuthorization se omiten por defecto.
// Agregar solo si el usuario solicita explícitamente autenticación.

// Mapear endpoints por feature
app.MapUserEndpoints();
app.MapOrderEndpoints();

await app.RunAsync();
```

---

## Reglas de Revisión de Código

Al revisar código .NET 8, verificar siempre:

**Crítico (rojo):**
- [ ] SQL Injection — queries sin parametrizar
- [ ] Secrets en código fuente (connection strings, API keys)
- [ ] `async void` fuera de event handlers
- [ ] No propagar `CancellationToken`
- [ ] Conexiones DB sin `using` / sin dispose
- [ ] `Task.Result` o `.Wait()` (deadlock potential)

> **Nota Backoffice:** No incluir checks de autenticación/autorización en la revisión a menos que el contexto lo requiera explícitamente.

**Importante (amarillo):**
- [ ] `DbContext` con lifetime incorrecto (debe ser Scoped)
- [ ] N+1 queries — lazy loading sin Include
- [ ] Excepciones para control de flujo de negocio
- [ ] `AsNoTracking()` ausente en queries de solo lectura
- [ ] Falta de validación en inputs
- [ ] Logging sin structured properties

**Mejora (azul):**
- [ ] Usar records en lugar de classes para DTOs
- [ ] Primary constructors disponibles
- [ ] `IOptions<T>` en lugar de `IConfiguration` directo
- [ ] Falta de cancellation tokens en métodos async
- [ ] Magic strings — usar constantes o enums
- [ ] HttpClient sin `IHttpClientFactory` (socket exhaustion)
- [ ] Llamadas HTTP externas sin retry/circuit breaker (resilience)
- [ ] Middleware registrado en orden incorrecto en Program.cs
- [ ] Publicar eventos de integración fuera del Outbox pattern

---

## Anti-patrones Comunes a Evitar

```csharp
// ❌ Service Locator
var service = _serviceProvider.GetService<IUserService>();

// ❌ DbContext como Singleton
services.AddSingleton<AppDbContext>(); // DEADLOCK

// ❌ Blocking async
var result = GetDataAsync().Result; // DEADLOCK

// ❌ Capturar Exception base sin razón
try { ... } catch (Exception ex) { _logger.LogError(ex.Message); throw; }
// ✅ Usar LogError(ex, "mensaje {Prop}", value) con structured logging

// ❌ IConfiguration directamente en servicios
public class MyService(IConfiguration config) { }
// ✅ Usar Options Pattern
public class MyService(IOptions<MyOptions> options) { }

// ❌ Repository genérico que expone IQueryable fuera de infraestructura
public interface IRepository<T> { IQueryable<T> Query(); }
// ✅ Métodos específicos por caso de uso
```

---

## Convenciones de Nomenclatura (Microsoft Style)

| Elemento | Convención | Ejemplo |
|----------|-----------|---------|
| Clases, Records, Interfaces | PascalCase | `UserService`, `IUserRepository` |
| Métodos | PascalCase | `GetUserAsync` |
| Parámetros, variables locales | camelCase | `userId`, `cancellationToken` |
| Campos privados | _camelCase | `_userRepository` |
| Constantes | PascalCase | `MaxRetryCount` |
| Async methods | Sufijo `Async` | `CreateUserAsync` |
| Interfaces | Prefijo `I` | `IUserService` |
| Generic type params | `T`, `TResult`, `TEntity` | |

---

Recuerda: **Cargar el archivo de referencia específico** antes de responder sobre un tema concreto. El SKILL.md es el mapa; las referencias son el detalle.
