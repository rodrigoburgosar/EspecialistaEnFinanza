# C# 12 Features — Guía de Uso en .NET 8

## Primary Constructors (C# 12)

```csharp
// ✅ Para servicios con inyección de dependencias
public class UserService(
    IUserRepository repository,
    IEmailService emailService,
    ILogger<UserService> logger)
{
    // Los parámetros son campos accesibles en toda la clase
    public async Task<Result<Guid>> CreateAsync(CreateUserDto dto, CancellationToken ct)
    {
        logger.LogInformation("Creating user {Email}", dto.Email);
        // usar repository, emailService directamente
    }
}

// ✅ Para records con validación
public record Email
{
    public string Value { get; }
    
    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.Contains('@')) throw new ArgumentException("Email inválido", nameof(value));
        Value = value.ToLowerInvariant();
    }
}
```

## Collection Expressions (C# 12)

```csharp
// Sintaxis unificada para todas las colecciones
int[] array = [1, 2, 3];
List<string> list = ["a", "b", "c"];
ImmutableArray<int> immutable = [1, 2, 3];
Span<int> span = [1, 2, 3];

// Spread operator
var combined = [..list1, ..list2, "extra"];
var withNew = [..existingItems, newItem];

// En métodos
static int[] GetIds() => [1, 2, 3, 4, 5];
```

## Pattern Matching avanzado

```csharp
// List patterns
static string Describe(int[] numbers) => numbers switch
{
    [] => "vacío",
    [var single] => $"uno: {single}",
    [var first, var second] => $"dos: {first}, {second}",
    [var first, .. var rest] => $"empieza con {first}, tiene {rest.Length} más",
};

// Property patterns anidados
if (order is { Status: OrderStatus.Pending, Customer: { IsPremium: true }, Total: > 1000 })
{
    ApplyPremiumDiscount(order);
}

// Switch expression con guard
var discount = order switch
{
    { Status: OrderStatus.Cancelled } => 0m,
    { Customer.IsPremium: true, Total: > 500 } => 0.15m,
    { Customer.IsPremium: true } => 0.10m,
    { Total: > 1000 } => 0.05m,
    _ => 0m
};

// Deconstruct en patterns
if (result is (true, var value))
    Process(value);
```

## Required Members (C# 11+)

```csharp
// ✅ Garantiza inicialización sin constructor
public class UserConfig
{
    public required string ApiKey { get; init; }
    public required string BaseUrl { get; init; }
    public int TimeoutSeconds { get; init; } = 30; // tiene default
}

// Fuerza al caller a proveer los required
var config = new UserConfig
{
    ApiKey = "abc",   // obligatorio
    BaseUrl = "https://api.example.com" // obligatorio
};
```

## Alias de tipos (C# 12)

```csharp
// Alias para tipos complejos
using OrderId = System.Guid;
using UserId = System.Guid;
using UserList = System.Collections.Generic.List<MyApp.Domain.User>;

// Alias para tuples
using Coordinate = (double Lat, double Lng);

public Coordinate GetUserLocation(UserId userId) => (40.7128, -74.0060);
```

## Inline Arrays (C# 12) — performance

```csharp
[System.Runtime.CompilerServices.InlineArray(4)]
public struct Buffer4<T>
{
    private T _element;
}

// Úsalo como array sin heap allocation
var buffer = new Buffer4<int>();
buffer[0] = 1;
buffer[1] = 2;
```

## Interceptors (C# 12, experimental)

```csharp
// Solo para source generators avanzados — no en código de aplicación normal
[InterceptsLocation("file.cs", line: 10, column: 5)]
public static void InterceptedMethod(this MyClass obj) { }
```

## Throwable expressions (C# 7+, uso moderno)

```csharp
// Null-coalescing throw
public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

// En expresiones condicionales
var value = TryGetValue(key) ?? throw new KeyNotFoundException($"Key '{key}' not found");

// ArgumentException helpers (C# built-ins .NET 6+)
ArgumentNullException.ThrowIfNull(user);
ArgumentException.ThrowIfNullOrEmpty(email);
ArgumentException.ThrowIfNullOrWhiteSpace(name);
ArgumentOutOfRangeException.ThrowIfNegative(age);
ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 150);
```

## File-scoped types (C# 11)

```csharp
// Tipos visibles solo en el archivo — perfecto para helpers internos
file class InternalHelper
{
    public static string Format(User user) => $"{user.Name} <{user.Email}>";
}

// Uso típico: implementaciones de interfaces en el mismo archivo que el endpoint
file sealed class GetUserEndpointHandler { }
```

## Global Using (C# 10+) — configurar en GlobalUsings.cs

```csharp
// GlobalUsings.cs en el proyecto API
global using MediatR;
global using FluentResults;
global using FluentValidation;
global using Microsoft.AspNetCore.Http.HttpResults;
global using Microsoft.EntityFrameworkCore;
global using System.Security.Claims;
```
