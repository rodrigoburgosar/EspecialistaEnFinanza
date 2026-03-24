# Middleware — ASP.NET Core 8 Best Practices

## Orden del pipeline — CRÍTICO

```csharp
// El orden importa — esta es la secuencia correcta según Microsoft
var app = builder.Build();

app.UseExceptionHandler();          // 1. Captura excepciones no manejadas
app.UseHsts();                       // 2. HSTS (solo producción)
app.UseHttpsRedirection();           // 3. Redirigir HTTP → HTTPS
app.UseStaticFiles();                // 4. Archivos estáticos (antes de routing)
app.UseRouting();                    // 5. Determina el endpoint (si usa UseEndpoints explícito)
app.UseCors();                       // 6. CORS — debe ir antes de auth
app.UseRateLimiter();                // 7. Rate limiting
app.UseAuthentication();             // 8. ¿Quién eres?
app.UseAuthorization();              // 9. ¿Qué puedes hacer?
app.UseOutputCache();                // 10. Output cache — después de auth
app.MapControllers();                // 11. Mapear endpoints
// o app.MapUserEndpoints();
```

## Formas de crear middleware

```csharp
// FORMA 1 — Middleware class (recomendada para middleware complejo)
// Usar cuando: tiene dependencias, lógica compleja, necesita ser testeado
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // Agregar al logging context para que aparezca en todos los logs del request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.LogDebug("Request {Method} {Path} [{CorrelationId}]",
                context.Request.Method, context.Request.Path, correlationId);
            await next(context);
        }
    }
}

// Extension method para registrar limpiamente
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}

// Registro en Program.cs
app.UseCorrelationId();
```

```csharp
// FORMA 2 — IMiddleware interface (recomendada cuando necesita DI con Scoped/Transient)
// Diferencia clave: IMiddleware se resuelve del DI container por cada request
public class RequestTimingMiddleware(ILogger<RequestTimingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        logger.LogInformation(
            "{Method} {Path} - {StatusCode} [{ElapsedMs}ms]",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds);
    }
}

// ✅ IMiddleware REQUIERE registro en DI
services.AddScoped<RequestTimingMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
```

```csharp
// FORMA 3 — Inline lambda (solo para middleware trivial / uno-a-uno)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Powered-By", "MyApp");
    await next(context);
});

// app.Run — terminal, no llama al siguiente
app.Run(async context =>
{
    await context.Response.WriteAsync("Endpoint de fallback");
});
```

## Middleware de manejo de excepciones (.NET 8)

```csharp
// Usar el built-in IExceptionHandler de .NET 8 — más limpio que middleware manual
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) 
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        logger.LogError(exception, "Excepción no manejada: {Message}", exception.Message);

        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problemDetails, ct);

        return true; // handled
    }
}

// Registro
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails();
app.UseExceptionHandler();
```

## Short-circuit middleware

```csharp
// .NET 8 — cortocircuitar sin llamar al siguiente middleware
app.MapGet("/health", () => Results.Ok())
    .ShortCircuit(); // Salta todo el pipeline (auth, etc.)

// O con status code específico
app.Map("/deprecated", () => { })
    .ShortCircuit(StatusCodes.Status410Gone);
```

## Middleware condicional

```csharp
// Solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Basado en condición dinámica
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyMiddleware>());

// MapWhen — branch separado del pipeline
app.MapWhen(
    context => context.Request.Headers.ContainsKey("X-Webhook"),
    appBuilder =>
    {
        appBuilder.UseMiddleware<WebhookValidationMiddleware>();
        appBuilder.Run(async ctx => await HandleWebhookAsync(ctx));
    });
```

## Request Body buffering (para leer el body múltiples veces)

```csharp
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger) 
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Habilitar buffering para poder releer el body
        context.Request.EnableBuffering();

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0; // reset para que el siguiente middleware lo lea

        logger.LogDebug("Request body: {Body}", body);
        await next(context);
    }
}
```
