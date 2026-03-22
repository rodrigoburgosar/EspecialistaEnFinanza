# Observability — Logging, Métricas y Trazas en .NET 8

## Serilog — structured logging

```csharp
// Program.cs
builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "MyApp")
    .WriteTo.Console(new JsonFormatter())  // JSON estructurado en producción
    .WriteTo.Seq(ctx.Configuration["Seq:Url"]!));  // centralizado

// ✅ SIEMPRE usar structured logging con propiedades nombradas
// ❌ Mal
_logger.LogInformation("Usuario " + userId + " creado");
_logger.LogError(ex.Message); // pierde el stack trace

// ✅ Bien
_logger.LogInformation("Usuario {UserId} creado con email {Email}", userId, email);
_logger.LogError(ex, "Error procesando orden {OrderId}", orderId); // incluye exception
```

## OpenTelemetry (.NET 8)

```csharp
// dotnet add package OpenTelemetry.Extensions.Hosting
// dotnet add package OpenTelemetry.Instrumentation.AspNetCore
// dotnet add package OpenTelemetry.Instrumentation.Http
// dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
// dotnet add package OpenTelemetry.Exporter.Otlp

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("MyApp")
        .AddAspNetCoreInstrumentation(opts =>
        {
            opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(opts =>
        {
            opts.SetDbStatementForText = true; // incluir SQL en dev
        })
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());
```

## Custom ActivitySource para trazas propias

```csharp
// Definir ActivitySource en la capa de aplicación
public static class Telemetry
{
    public static readonly ActivitySource ActivitySource = new("MyApp", "1.0.0");
}

// Usar en handlers
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken ct)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("CreateUser");
        activity?.SetTag("user.email", command.Email);

        var result = await CreateInternalAsync(command, ct);

        if (result.IsFailed)
            activity?.SetStatus(ActivityStatusCode.Error, result.Errors.First().Message);
        else
            activity?.SetTag("user.id", result.Value);

        return result;
    }
}
```

## Métricas personalizadas (.NET 8 — System.Diagnostics.Metrics)

```csharp
// Definir métricas
public class OrderMetrics
{
    private readonly Counter<long> _ordersCreated;
    private readonly Histogram<double> _orderProcessingDuration;
    private readonly ObservableGauge<int> _pendingOrders;
    private int _pendingOrdersCount;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MyApp.Orders");
        
        _ordersCreated = meter.CreateCounter<long>(
            "orders.created",
            unit: "{orders}",
            description: "Total de órdenes creadas");

        _orderProcessingDuration = meter.CreateHistogram<double>(
            "orders.processing.duration",
            unit: "ms",
            description: "Duración del procesamiento de órdenes");

        _pendingOrders = meter.CreateObservableGauge(
            "orders.pending",
            () => _pendingOrdersCount,
            unit: "{orders}");
    }

    public void RecordOrderCreated(string status) =>
        _ordersCreated.Add(1, new("status", status));

    public IDisposable MeasureProcessing() =>
        new DurationMeasurer(_orderProcessingDuration);
}

// Registro
services.AddSingleton<OrderMetrics>();
services.AddMetrics();
```

## Health Checks

```csharp
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, "redis")
    .AddUrlGroup(new Uri("https://api.external.com/health"), "external-api")
    .AddCheck<CustomBusinessHealthCheck>("business-rules");

// Endpoints con detalle diferenciado
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // Solo verifica que el proceso está vivo
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse  // JSON detallado
});
```

## Request Logging Middleware

```csharp
// Usar Serilog.AspNetCore para loguear cada request automáticamente
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
    };

    // No loguear health checks
    options.GetLevel = (ctx, _, ex) =>
        ex != null || ctx.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : ctx.Request.Path.StartsWithSegments("/health")
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
});
```
