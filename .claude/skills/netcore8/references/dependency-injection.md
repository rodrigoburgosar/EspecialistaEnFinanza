# Dependency Injection — .NET 8 Best Practices

## Lifetimes — regla de oro

| Lifetime | Cuándo usar | Pitfall |
|----------|------------|---------|
| **Singleton** | Estado compartido, caches, configuración | No inyectar Scoped/Transient dentro |
| **Scoped** | Por request HTTP, DbContext, Unit of Work | No usar fuera de un scope (background services) |
| **Transient** | Servicios stateless, ligeros | Evitar con recursos costosos (DB connections) |

```csharp
// ❌ Captive dependency — Singleton captura Scoped → el Scoped se vuelve Singleton
services.AddSingleton<MyService>(); // MyService depende de IDbContext (Scoped) → BUG

// ✅ Correcto: factory para resolver Scoped dentro de Singleton
services.AddSingleton<MyService>();
// En MyService:
public class MyService(IServiceScopeFactory scopeFactory)
{
    public async Task DoWork(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // usar dbContext...
    }
}
```

## Options Pattern — siempre preferir sobre IConfiguration

```csharp
// Clase de opciones
public class EmailOptions
{
    public const string SectionName = "Email";

    [Required] public string SmtpHost { get; init; } = string.Empty;
    [Required, Range(1, 65535)] public int SmtpPort { get; init; }
    [Required] public string FromAddress { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
}

// Registro con validación en startup
services.AddOptions<EmailOptions>()
    .BindConfiguration(EmailOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart(); // ✅ Falla en startup si config inválida, no en runtime

// Uso — IOptions<T> para singleton, IOptionsSnapshot<T> para scoped con recarga
public class EmailService(IOptions<EmailOptions> options) 
{
    private readonly EmailOptions _options = options.Value;
}

// IOptionsMonitor<T> para recargar sin reiniciar (appsettings live reload)
public class FeatureFlagService(IOptionsMonitor<FeatureFlags> monitor)
{
    public bool IsEnabled(string feature) => 
        monitor.CurrentValue.Features.GetValueOrDefault(feature, false);
}
```

## Keyed Services (.NET 8)

```csharp
// Registrar múltiples implementaciones de la misma interfaz por clave
services.AddKeyedScoped<IPaymentGateway, StripeGateway>("stripe");
services.AddKeyedScoped<IPaymentGateway, PaypalGateway>("paypal");

// Resolver por clave
public class PaymentService(
    [FromKeyedServices("stripe")] IPaymentGateway stripeGateway,
    [FromKeyedServices("paypal")] IPaymentGateway paypalGateway) { }

// O dinámicamente
public class PaymentRouter(IServiceProvider sp)
{
    public IPaymentGateway GetGateway(string provider) =>
        sp.GetRequiredKeyedService<IPaymentGateway>(provider);
}
```

## Registro automático con Scrutor

```csharp
// dotnet add package Scrutor

// Auto-registrar por convención — evita registros manuales repetitivos
services.Scan(scan => scan
    .FromAssemblyOf<UserRepository>()
    .AddClasses(classes => classes.InNamespaceOf<UserRepository>())
    .AsImplementedInterfaces()
    .WithScopedLifetime());

// O más específico
services.Scan(scan => scan
    .FromAssemblyOf<UserRepository>()
    .AddClasses(classes => classes.AssignableTo(typeof(IRepository<>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

## HttpClient con IHttpClientFactory

```csharp
// ❌ Nunca instanciar HttpClient directamente — socket exhaustion
var client = new HttpClient(); // INCORRECTO

// ✅ Usar IHttpClientFactory siempre
// Named client
services.AddHttpClient("payments", client =>
{
    client.BaseAddress = new Uri("https://api.payments.com");
    client.DefaultRequestHeaders.Add("X-Api-Key", "...");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(); // .NET 8 — retry + circuit breaker automáticos

// Typed client (preferido)
services.AddHttpClient<IPaymentsApiClient, PaymentsApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Payments:BaseUrl"]!);
})
.AddStandardResilienceHandler()
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});
```

## Background Services

```csharp
// Usar BackgroundService como base
public class OrderProcessingService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Order processing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOrdersAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown normal — no loguear como error
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing orders");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // backoff
            }
        }
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken ct)
    {
        // ✅ Crear scope para cada iteración — DbContext es Scoped
        await using var scope = scopeFactory.CreateAsyncScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        await orderService.ProcessPendingAsync(ct);
    }
}

// Registro
services.AddHostedService<OrderProcessingService>();
```

## Decorator Pattern con DI

```csharp
// Decorar servicios con Scrutor
services.AddScoped<IUserRepository, UserRepository>();
services.Decorate<IUserRepository, CachedUserRepository>();
services.Decorate<IUserRepository, LoggedUserRepository>();

// El orden de ejecución: LoggedUserRepository → CachedUserRepository → UserRepository
```
