using StockAnalyzer.Agentes;
using StockAnalyzer.Api.Datos;
using StockAnalyzer.Contratos.Interfaces;
using StockAnalyzer.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// ── Caché en memoria ─────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Servicios HTTP ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("AlphaVantage", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("NlpService", c => c.Timeout = TimeSpan.FromSeconds(15));

// ── Agentes ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAgentePrecios, AgentePrecios>();
builder.Services.AddScoped<IAgenteSeñalesTecnicas, AgenteSeñalesTecnicas>();
builder.Services.AddScoped<IAgenteNoticias, AgenteNoticias>();
builder.Services.AddScoped<IAgenteDecision, AgenteDecision>();
builder.Services.AddSingleton<IAgenteNotificacion, AgenteNotificacion>();

// ── Orquestador ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<StockAnalyzer.Orquestador.Orquestador>();

// ── Persistencia ────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ContextoBd>(o =>
    o.UseSqlite("Data Source=stock_analyzer.db"));
builder.Services.AddScoped<IRepositorioRecomendaciones, RepositorioRecomendaciones>();

// ── Scheduler ───────────────────────────────────────────────────────────────
builder.Services.AddHostedService<TrabajadorAnalisis>();

var host = builder.Build();
host.Run();
