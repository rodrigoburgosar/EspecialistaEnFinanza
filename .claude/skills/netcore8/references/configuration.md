# Configuration — .NET 8 Best Practices

## Jerarquía de configuración (mayor prioridad gana)

```
1. Command-line arguments          (más alta prioridad)
2. Variables de entorno
3. User Secrets (solo desarrollo)
4. appsettings.{Environment}.json
5. appsettings.json                (menor prioridad)
```

## appsettings.json — estructura correcta

```json
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Database=myapp;Username=user;Password=...",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Issuer": "https://myapp.com",
    "Audience": "https://myapp.com",
    "ExpirationMinutes": 60
  },
  "Email": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "FromAddress": "noreply@myapp.com",
    "UseSsl": true
  },
  "Features": {
    "EnableNewCheckout": false,
    "MaxUploadSizeMb": 10
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

```json
// appsettings.Development.json — sobreescribe valores para dev
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Database=myapp_dev;Username=postgres;Password=postgres"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
      }
    }
  }
}
```

## Secrets — gestión correcta por entorno

```bash
# Desarrollo — User Secrets (nunca comitear)
dotnet user-secrets init
dotnet user-secrets set "Jwt:SecretKey" "dev-secret-key-min-32-chars-long!!"
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;..."

# Verificar
dotnet user-secrets list
```

```csharp
// Program.cs — User Secrets se agregan automáticamente en Development
// No se necesita código adicional — WebApplication.CreateBuilder lo hace por defecto

// Para producción: variables de entorno con double-underscore para secciones
// JWT__SecretKey=production-secret
// ConnectionStrings__Database=Host=prod-server;...

// Azure Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/"),
    new DefaultAzureCredential());

// AWS Secrets Manager
builder.Configuration.AddSecretsManager(region: RegionEndpoint.USEast1, configurator: opts =>
{
    opts.SecretFilter = entry => entry.Name.StartsWith("myapp/");
    opts.KeyGenerator = (entry, key) => key.Replace("myapp/", "").Replace("/", ":");
});
```

## Options Pattern completo

```csharp
// 1. Clase de opciones con validación
public class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, 100)]
    public int MaxRetryCount { get; init; } = 3;

    [Range(1, 3600)]
    public int CommandTimeoutSeconds { get; init; } = 30;

    public bool EnableSensitiveDataLogging { get; init; } = false;
}

// 2. Registro con validación en startup (falla rápido)
builder.Services.AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart(); // ✅ Falla al arrancar, no en primer uso

// 3. Registro compacto (extensión recomendada)
// En DependencyInjection.cs de Infrastructure:
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<DatabaseOptions>()
        .BindConfiguration(DatabaseOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddOptions<EmailOptions>()
        .BindConfiguration(EmailOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    return services;
}

// 4. Cuándo usar cada variante de IOptions:
// IOptions<T>         → valor fijo al arrancar, singleton, sin recarga
// IOptionsSnapshot<T> → recarga por request (Scoped), para appsettings con live reload
// IOptionsMonitor<T>  → recarga en tiempo real (Singleton), notifica cambios

public class CacheService(IOptionsMonitor<CacheOptions> monitor)
{
    // Se actualiza automáticamente si cambia appsettings en caliente
    private CacheOptions Options => monitor.CurrentValue;
}
```

## Feature Flags

```csharp
// Opciones para feature flags
public class FeatureFlags
{
    public const string SectionName = "Features";
    public bool EnableNewCheckout { get; init; }
    public bool EnableBetaApi { get; init; }
    public int MaxUploadSizeMb { get; init; } = 10;
}

// Uso en código
public class CheckoutService(IOptions<FeatureFlags> features)
{
    public IActionResult Checkout()
    {
        if (!features.Value.EnableNewCheckout)
            return RedirectToLegacyCheckout();

        return ProcessNewCheckout();
    }
}

// Para feature flags avanzados con % rollout → usar Microsoft.FeatureManagement
// dotnet add package Microsoft.FeatureManagement.AspNetCore
builder.Services.AddFeatureManagement(builder.Configuration.GetSection("FeatureManagement"));
```

## Configuración por ambiente

```csharp
// Múltiples ambientes: Development, Staging, Production
// Agregar appsettings.Staging.json para staging-específico

// Verificar ambiente en código
if (builder.Environment.IsProduction())
{
    // Solo en producción
    builder.Services.AddApplicationInsightsTelemetry();
}

if (builder.Environment.IsDevelopment())
{
    // Solo en desarrollo
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

// Ambiente personalizado (además de los tres estándar)
// ASPNETCORE_ENVIRONMENT=Staging
builder.Environment.EnvironmentName == "Staging"
```

## Configuración de Kestrel

```json
// appsettings.json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8080"
      },
      "Https": {
        "Url": "https://0.0.0.0:8443",
        "Certificate": {
          "Path": "/certs/cert.pfx",
          "Password": "cert-password"
        }
      },
      "Grpc": {
        "Url": "https://0.0.0.0:9090",
        "Protocols": "Http2"
      }
    },
    "Limits": {
      "MaxRequestBodySize": 10485760,
      "MaxConcurrentConnections": 1000
    }
  }
}
```
