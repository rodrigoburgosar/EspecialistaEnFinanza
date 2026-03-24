# Performance & Caching — .NET 8 Best Practices

## Output Cache (.NET 8 built-in)

```csharp
// Registro
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Cache());

    options.AddPolicy("UsersCache", builder => builder
        .Cache()
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("users")
        .SetVaryByQuery("page", "pageSize"));
});

app.UseOutputCache();

// En endpoints
app.MapGet("/users", GetUsersAsync)
    .CacheOutput("UsersCache");

// Invalidar tag cuando hay cambios
app.MapPost("/users", async (CreateUserRequest req, IOutputCacheStore cache, ...) =>
{
    // crear usuario...
    await cache.EvictByTagAsync("users", CancellationToken.None);
    return TypedResults.Created(...);
});
```

## HybridCache (.NET 9 preview, disponible en .NET 8 via NuGet)

```csharp
// dotnet add package Microsoft.Extensions.Caching.Hybrid

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024; // 1MB
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});

// Uso — reemplaza el patrón GetOrCreateAsync
public class UserService(HybridCache cache, IUserRepository repo)
{
    public async Task<UserResponse?> GetUserAsync(Guid id, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            $"user:{id}",
            async token => await repo.GetByIdAsync(id, token),
            cancellationToken: ct);
}
```

## IMemoryCache — para cache local

```csharp
public class CachedUserRepository(
    IUserRepository inner,
    IMemoryCache cache,
    ILogger<CachedUserRepository> logger) : IUserRepository
{
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2),
        Size = 1
    };

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"user:{id}";

        if (cache.TryGetValue(cacheKey, out User? cachedUser))
        {
            logger.LogDebug("Cache hit for user {UserId}", id);
            return cachedUser;
        }

        var user = await inner.GetByIdAsync(id, ct);
        if (user is not null)
            cache.Set(cacheKey, user, CacheOptions);

        return user;
    }
}
```

## IDistributedCache con Redis

```csharp
// dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "myapp:";
});

// Extension methods tipados sobre IDistributedCache
public static class DistributedCacheExtensions
{
    public static async Task<T?> GetAsync<T>(
        this IDistributedCache cache, 
        string key, 
        CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }

    public static async Task SetAsync<T>(
        this IDistributedCache cache,
        string key, T value,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(5)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await cache.SetAsync(key, bytes, options, ct);
    }
}
```

## System.Threading.Channels — productor/consumidor

```csharp
// Para procesar work items de forma asíncrona sin bloquear requests
public class BackgroundJobQueue(int capacity = 100) : IBackgroundJobQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _channel =
        Channel.CreateBounded<Func<CancellationToken, ValueTask>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public async ValueTask EnqueueAsync(Func<CancellationToken, ValueTask> job, CancellationToken ct = default) =>
        await _channel.Writer.WriteAsync(job, ct);

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct) =>
        await _channel.Reader.ReadAsync(ct);
}

// Worker que consume la queue
public class BackgroundJobWorker(IBackgroundJobQueue queue, ILogger<BackgroundJobWorker> logger) 
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ✅ Usar DequeueAsync de la interfaz — no ReadAllAsync (no está en IBackgroundJobQueue)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await queue.DequeueAsync(stoppingToken);
                await job.Invoke(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error processing job"); }
        }
    }
}
```

## Span<T> y Memory<T> para procesamiento sin allocations

```csharp
// ✅ Usar Span para operaciones sobre strings/arrays sin allocar
public static bool TryParseUserId(ReadOnlySpan<char> input, out Guid userId)
{
    // Operación sin string allocation
    return Guid.TryParse(input, out userId);
}

// ✅ ArrayPool para buffers temporales
public async Task<byte[]> ProcessDataAsync(Stream input, CancellationToken ct)
{
    const int bufferSize = 4096;
    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    try
    {
        var bytesRead = await input.ReadAsync(buffer.AsMemory(0, bufferSize), ct);
        return ProcessBuffer(buffer.AsSpan(0, bytesRead));
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
```

## Frozen Collections (.NET 8) — para datos inmutables frecuentemente leídos

```csharp
// FrozenDictionary / FrozenSet — optimizados para lectura, inmutables
private static readonly FrozenDictionary<string, int> CountryCodes =
    new Dictionary<string, int>
    {
        ["CL"] = 56, ["AR"] = 54, ["BR"] = 55, ["MX"] = 52
    }.ToFrozenDictionary();

// ~30% más rápido en lookups que Dictionary normal
var code = CountryCodes.TryGetValue("CL", out var c) ? c : 0;
```

## Benchmarking con BenchmarkDotNet

```csharp
// dotnet add package BenchmarkDotNet

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StringProcessingBenchmarks
{
    [Benchmark(Baseline = true)]
    public string StringConcat() => "Hello" + " " + "World";

    [Benchmark]
    public string StringInterpolation() => $"Hello World";

    [Benchmark]
    public string SpanBased()
    {
        Span<char> buffer = stackalloc char[11];
        "Hello".AsSpan().CopyTo(buffer);
        " ".AsSpan().CopyTo(buffer[5..]);
        "World".AsSpan().CopyTo(buffer[6..]);
        return new string(buffer);
    }
}
```
