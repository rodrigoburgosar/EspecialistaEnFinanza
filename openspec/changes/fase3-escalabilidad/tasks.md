## 1. Multi-ticker — Caché y configuración

- [x] 1.1 Agregar `IMemoryCache` al DI en `Program.cs` de Api y Worker
- [x] 1.2 Implementar caché en memoria con TTL 4h en `AgentePrecios` usando `IMemoryCache`
- [x] 1.3 Ampliar ventana de precios de 14 a 25 días en `AgentePrecios` (requerido para Bollinger Bands)
- [x] 1.4 Agregar más tickers a `config/tickers.yaml` (MSFT, NVDA, GOOGL)
- [x] 1.5 Verificar que `TrabajadorAnalisis` itera correctamente sobre todos los tickers activos
- [ ] 1.6 Validar que el error de un ticker no interrumpe el análisis de los demás (ya implementado — verificar con prueba manual)

## 2. Indicadores adicionales — Bollinger Bands y EMA

- [x] 2.1 Extender record `SeñalesTecnicas` con: `BollingerSuperior`, `BollingerInferior`, `CercaBandaInferior`, `CercaBandaSuperior`, `EMA20`, `TendenciaAlcista`
- [x] 2.2 Implementar cálculo de Bollinger Bands (20 períodos, 2σ) en `AgenteSeñalesTecnicas` usando `Skender`
- [x] 2.3 Implementar cálculo de EMA 20 en `AgenteSeñalesTecnicas` usando `Skender`
- [x] 2.4 Retornar valores por defecto si hay menos de 20 cotizaciones (sin lanzar excepción)
- [x] 2.5 Actualizar tabla de decisión en `AgenteDecision`:
  - RSI<30 + sentimiento>0.3 + CercaBandaInferior → COMPRAR / ALTA
  - RSI<30 + sentimiento>0.3 (sin Bollinger) → COMPRAR / MEDIA
  - RSI>70 + sentimiento<-0.3 + CercaBandaSuperior → VENDER / ALTA
  - RSI>70 + sentimiento<-0.3 (sin Bollinger) → VENDER / MEDIA
  - Todo lo demás → MANTENER / MEDIA

## 3. PostgreSQL — Migración de persistencia

- [x] 3.1 Agregar NuGet `Npgsql.EntityFrameworkCore.PostgreSQL` al proyecto `StockAnalyzer.Api`
- [x] 3.2 Actualizar `ContextoBd` para usar `UseNpgsql` en producción y `UseSqlite` en Development
- [x] 3.3 Agregar `DATABASE_URL` a `.env.example` y `appsettings.json`
- [x] 3.4 Reemplazar `db.Database.EnsureCreated()` por `db.Database.Migrate()` en `Program.cs`
- [x] 3.5 Crear migración inicial con `dotnet ef migrations add MigracionInicial`
- [ ] 3.6 Aplicar migración contra PostgreSQL y verificar tabla `Recomendaciones` creada correctamente

## 4. Dashboard web — Razor Pages

- [x] 4.1 Agregar Razor Pages al proyecto `StockAnalyzer.Api` (`builder.Services.AddRazorPages()`)
- [x] 4.2 Crear `Pages/Dashboard/Index.cshtml` con tabla de historial de recomendaciones
- [x] 4.3 Crear `Pages/Dashboard/Index.cshtml.cs` con PageModel que consulta `IRepositorioRecomendaciones`
- [x] 4.4 Agregar filtros por ticker y por acción al PageModel
- [x] 4.5 Extender `IRepositorioRecomendaciones` con método `ObtenerHistorialAsync(string? ticker, string? accion, int cantidad)`
- [x] 4.6 Implementar `ObtenerHistorialAsync` en `RepositorioRecomendaciones`
- [x] 4.7 Agregar tarjetas de resumen por ticker (última acción + últimas 5 señales)
- [x] 4.8 Registrar ruta del dashboard en `Program.cs` con `app.MapRazorPages()`
- [ ] 4.9 Verificar dashboard en `http://localhost:5000/dashboard`

## 5. Verificación end-to-end

- [ ] 5.1 Ejecutar análisis manual para 2 tickers distintos y verificar recomendaciones independientes en BD
- [ ] 5.2 Verificar que caché evita llamadas duplicadas a Alpha Vantage en el mismo ciclo
- [ ] 5.3 Verificar que Bollinger Bands amplían/reducen correctamente el nivel de confianza
- [ ] 5.4 Navegar a `/dashboard` y verificar historial con filtros funcionando
- [ ] 5.5 Verificar que migraciones PostgreSQL se aplican correctamente al iniciar
