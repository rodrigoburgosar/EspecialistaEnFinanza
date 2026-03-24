# Minimal APIs — .NET 8 Best Practices

## Estructura recomendada: IEndpoint pattern

```csharp
// Contrato base
public interface IEndpoint
{
    void MapEndpoints(IEndpointRouteBuilder app);
}

// Extension method para auto-registrar todos los endpoints
public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpointTypes = assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpoint)) && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in endpointTypes)
            services.AddScoped(typeof(IEndpoint), type);

        return services;
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();
        var group = app.MapGroup(string.Empty);
        foreach (var endpoint in endpoints)
            endpoint.MapEndpoints(group);
        return app;
    }
}
```

## Implementación de endpoints por feature

```csharp
// Users/GetUserEndpoint.cs
internal sealed class GetUserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/users/{id:guid}", HandleAsync)
            .WithName("GetUser")
            .WithSummary("Obtiene un usuario por ID")
            .WithTags("Users")
            .Produces<UserResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    // Handler como método estático (mejor performance, evita captura de this)
    private static async Task<Results<Ok<UserResponse>, NotFound<ProblemDetails>>> HandleAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserQuery(id), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.NotFound(new ProblemDetails { Title = result.Error });
    }
}
```

## TypedResults — siempre preferir sobre Results

```csharp
// ❌ Results no tipado — sin info en Swagger
app.MapGet("/users/{id}", async (Guid id, ...) =>
{
    return Results.Ok(user); // Swagger no sabe el tipo
});

// ✅ TypedResults — Swagger infiere tipos automáticamente
app.MapGet("/users/{id}", async (Guid id, ...) 
    : Task<Results<Ok<UserResponse>, NotFound>> =>
{
    if (user is null) return TypedResults.NotFound();
    return TypedResults.Ok(user.ToResponse());
});
```

## Route Groups con configuración compartida

```csharp
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/users")
            .WithTags("Users")
            .RequireAuthorization()
            .WithOpenApi();

        group.MapGet("/", GetAllUsersAsync);
        group.MapGet("/{id:guid}", GetUserByIdAsync);
        group.MapPost("/", CreateUserAsync)
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>();
        group.MapPut("/{id:guid}", UpdateUserAsync);
        group.MapDelete("/{id:guid}", DeleteUserAsync)
            .RequireAuthorization("admin");

        return group;
    }
}
```

## Filtros de endpoint

```csharp
// Filtro genérico de validación con FluentValidation
public class ValidationFilter<TRequest>(IValidator<TRequest> validator) 
    : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            return Results.BadRequest("Request inválido");

        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        return await next(context);
    }
}

// Registro en endpoint
app.MapPost("/users", CreateUserAsync)
    .AddEndpointFilter<ValidationFilter<CreateUserRequest>>();
```

## Binding de parámetros

```csharp
// .NET 8 soporta binding desde múltiples fuentes
app.MapPost("/users/{orgId:guid}/invite", async (
    [FromRoute] Guid orgId,                         // ruta
    [FromBody] InviteUserRequest request,           // body (automático en POST)
    [FromQuery] string? redirectUrl,                // query string
    [FromHeader(Name = "X-Correlation-Id")] string? correlationId, // header
    [FromServices] ISender sender,                  // DI (automático)
    CancellationToken cancellationToken             // DI (automático)
) => { });

// AsParameters — binding desde clase/record
app.MapGet("/users", async ([AsParameters] GetUsersRequest request, ISender sender, CancellationToken ct)
    => await sender.Send(new GetUsersQuery(request.Page, request.PageSize, request.Search), ct));

public record GetUsersRequest(
    [FromQuery] int Page = 1,
    [FromQuery] int PageSize = 20,
    [FromQuery] string? Search = null);
```

## Versionado de API

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

// Grupos versionados
var v1 = app.NewVersionedApi("Users")
    .MapGroup("/api/v{version:apiVersion}/users")
    .HasApiVersion(1);

var v2 = app.NewVersionedApi("Users")
    .MapGroup("/api/v{version:apiVersion}/users")
    .HasApiVersion(2);
```

## Problem Details — respuestas de error estándar RFC 7807

```csharp
// Program.cs — .NET 8 built-in
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
    };
});

app.UseExceptionHandler();
app.UseStatusCodePages();

// Respuesta automática en formato RFC 7807:
// {
//   "type": "https://tools.ietf.org/html/rfc7807",
//   "title": "Not Found",
//   "status": 404,
//   "traceId": "00-abc123...",
//   "timestamp": "2024-01-15T10:30:00Z"
// }
```
