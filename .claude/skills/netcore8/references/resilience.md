# Resilience — Polly + Microsoft.Extensions.Resilience (.NET 8)

## Microsoft.Extensions.Resilience (nativo .NET 8)

```csharp
// dotnet add package Microsoft.Extensions.Http.Resilience
// Este paquete viene integrado con .NET 8 — preferir sobre Polly directo

// Configuración estándar — combina retry + circuit breaker + timeout + rate limiter
builder.Services.AddHttpClient<IOrdersApiClient, OrdersApiClient>(client =>
    client.BaseAddress = new Uri(configuration["Orders:BaseUrl"]!))
    .AddStandardResilienceHandler(options =>
    {
        // Retry: 3 intentos con backoff exponencial + jitter
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true; // evita thundering herd
        options.Retry.Delay = TimeSpan.FromSeconds(1);

        // Circuit breaker: abre si 50% de requests fallan en 10s
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        // Timeout por intento
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

        // Timeout total (todos los reintentos)
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
    });
```

## Pipeline de resiliencia personalizado

```csharp
// Para operaciones que no son HTTP (DB calls, servicios externos, etc.)
builder.Services.AddResiliencePipeline("database-pipeline", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200),
            // Solo reintentar en errores transitorios
            ShouldHandle = new PredicateBuilder()
                .Handle<TimeoutException>()
                .Handle<SqlException>(ex => ex.IsTransient)
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.3,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(60),
            OnOpened = args =>
            {
                logger.LogWarning("Circuit breaker abierto por {Duration}", args.BreakDuration);
                return default;
            }
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});

// Uso
public class UserRepository(
    AppDbContext context,
    ResiliencePipelineProvider<string> pipelineProvider) : IUserRepository
{
    private readonly ResiliencePipeline _pipeline = 
        pipelineProvider.GetPipeline("database-pipeline");

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _pipeline.ExecuteAsync(
            async token => await context.Users.FindAsync([id], token),
            ct);
}
```

## Polly directo — para casos avanzados

```csharp
// dotnet add package Polly.Core (ya incluido via Microsoft.Extensions.Resilience)

// Hedging strategy — envía request a múltiples endpoints, toma el primero
builder.Services.AddHttpClient<IPaymentGateway, PaymentGateway>()
    .AddResilienceHandler("hedging", pipeline =>
    {
        pipeline.AddHedging(new HedgingStrategyOptions<HttpResponseMessage>
        {
            MaxHedgedAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(500), // intenta paralelo si no responde en 500ms
            ShouldHandle = args => args.Outcome switch
            {
                { Exception: HttpRequestException } => PredicateResult.True(),
                { Result.StatusCode: HttpStatusCode.TooManyRequests } => PredicateResult.True(),
                _ => PredicateResult.False()
            }
        });
    });
```

## Idempotency — para proteger contra reintentos duplicados

```csharp
// Middleware de idempotencia para POST/PUT
public class IdempotencyMiddleware(
    IDistributedCache cache,
    ILogger<IdempotencyMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Solo aplicar a métodos no idempotentes
        if (context.Request.Method is not ("POST" or "PATCH"))
        {
            await next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";
        var cached = await cache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            logger.LogInformation("Returning cached response for idempotency key {Key}", idempotencyKey);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cached);
            return;
        }

        // Capturar response
        var originalBody = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await next(context);

        responseBody.Seek(0, SeekOrigin.Begin);
        var responseContent = await new StreamReader(responseBody).ReadToEndAsync();

        // Solo cachear respuestas exitosas
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            await cache.SetStringAsync(cacheKey, responseContent, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
        }

        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBody);
    }
}
```

## Timeout con CancellationToken linking

```csharp
// Combinar timeout propio + cancellation del cliente
public async Task<Data> FetchExternalDataAsync(CancellationToken clientCancellation)
{
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        clientCancellation, timeoutCts.Token);

    try
    {
        return await _httpClient.GetFromJsonAsync<Data>("/data", linkedCts.Token)
            ?? throw new InvalidOperationException("Respuesta vacía");
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
    {
        throw new TimeoutException("La operación excedió el tiempo límite de 10 segundos");
    }
}
```
