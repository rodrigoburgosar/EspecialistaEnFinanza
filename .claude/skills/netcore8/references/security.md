# Security — ASP.NET Core 8 Best Practices

## JWT Authentication

```csharp
// Program.cs
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),
            ClockSkew = TimeSpan.FromMinutes(1) // ✅ Reducir del default de 5 min
        };

        // Para SignalR / WebSockets
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });
```

## Authorization Policies

```csharp
// Definir políticas semánticas — no hardcodear roles en endpoints
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("Admin"));

    options.AddPolicy("CanManageOrders", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Manager")));

    options.AddPolicy("ResourceOwner", policy =>
        policy.AddRequirements(new ResourceOwnerRequirement()));

    // Política por defecto — cualquier usuario autenticado
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Fallback policy — rutas no decoradas requieren auth
    options.FallbackPolicy = options.DefaultPolicy;
});

// Custom requirement
public record ResourceOwnerRequirement : IAuthorizationRequirement;

// ✅ IMPORTANTE: registrar el handler en DI — sin esto la policy nunca se evalúa
// services.AddSingleton<IAuthorizationHandler, ResourceOwnerHandler>();

public class ResourceOwnerHandler(IHttpContextAccessor httpContext)
    : AuthorizationHandler<ResourceOwnerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var resourceOwnerId = httpContext.HttpContext?.GetRouteValue("userId")?.ToString();

        if (userId == resourceOwnerId || context.User.IsInRole("Admin"))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

## Refresh Tokens

```csharp
public class TokenService(IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Refresh token = 32 bytes de entropía, no un JWT
    public string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
```

## Rate Limiting (.NET 8 built-in)

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Política global — sliding window
    options.AddSlidingWindowLimiter("global", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 4;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    // Política por IP para login
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                SegmentsPerWindow = 5
            }));
});

app.UseRateLimiter();

// Aplicar en endpoints
app.MapPost("/auth/login", LoginAsync)
    .RequireRateLimiting("login")
    .AllowAnonymous();
```

## Secrets — nunca en código ni appsettings

```csharp
// ✅ Usar User Secrets en desarrollo
// dotnet user-secrets set "Jwt:SecretKey" "mi-clave-secreta"

// ✅ Variables de entorno en producción
// JWT__SecretKey=... (doble underscore para secciones anidadas)

// ✅ Azure Key Vault / AWS Secrets Manager para producción
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/"),
    new DefaultAzureCredential());

// ❌ NUNCA esto:
// "Jwt": { "SecretKey": "mi-clave-super-secreta" } en appsettings.json
```

## CORS

```csharp
// Configuración restrictiva por defecto
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
        policy
            .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()!)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type")
            .WithExposedHeaders("X-Total-Count")
            .SetPreflightMaxAge(TimeSpan.FromHours(1)));
});

// ❌ Nunca en producción:
// policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
```

## Anti-forgery / HTTPS

```csharp
// Forzar HTTPS en producción
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // HSTS header
}
app.UseHttpsRedirection();

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```
