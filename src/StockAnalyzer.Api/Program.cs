using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Agentes;
using StockAnalyzer.Api.Datos;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Orquestador;

var builder = WebApplication.CreateBuilder(args);

// ── Configuración ───────────────────────────────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// ── Caché en memoria ─────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Servicios HTTP ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("AlphaVantage", cliente =>
    cliente.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("NlpService", cliente =>
    cliente.Timeout = TimeSpan.FromSeconds(15));

// ── Agentes ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAgentePrecios, AgentePrecios>();
builder.Services.AddScoped<IAgenteSeñalesTecnicas, AgenteSeñalesTecnicas>();
builder.Services.AddScoped<IAgenteNoticias, AgenteNoticias>();
builder.Services.AddScoped<IAgenteDecision, AgenteDecision>();
builder.Services.AddSingleton<IAgenteNotificacion, AgenteNotificacion>();

// ── Orquestador ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<Orquestador>();

// ── Persistencia ─────────────────────────────────────────────────────────────
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Postgres");

if (!string.IsNullOrWhiteSpace(databaseUrl) && !builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ContextoBd>(opciones =>
        opciones.UseNpgsql(databaseUrl));
}
else
{
    builder.Services.AddDbContext<ContextoBd>(opciones =>
        opciones.UseSqlite("Data Source=stock_analyzer.db"));
}

builder.Services.AddScoped<IRepositorioRecomendaciones, RepositorioRecomendaciones>();

// ── API y Razor Pages ────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
    opciones.SwaggerDoc("v1", new()
    {
        Title = "StockAnalyzer API",
        Version = "v1",
        Description = "Sistema multi-agente de análisis y recomendación de acciones bursátiles."
    }));

var app = builder.Build();

// ── Migraciones automáticas al iniciar ──────────────────────────────────────
using (var alcance = app.Services.CreateScope())
{
    var db = alcance.ServiceProvider.GetRequiredService<ContextoBd>();
    db.Database.Migrate();
}

// ── Pipeline HTTP ─────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opciones =>
        opciones.SwaggerEndpoint("/swagger/v1/swagger.json", "StockAnalyzer API v1"));
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();

app.Run();
