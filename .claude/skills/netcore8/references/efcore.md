# Entity Framework Core 8 — Best Practices

## DbContext configuration

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Auto-aplicar todas las configuraciones en el assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Convención global: propiedades string no nullable = varchar(200) por default
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() == null))
        {
            property.SetMaxLength(200);
        }
    }

    // Interceptor para auditoría automática
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

## Configuraciones con IEntityTypeConfiguration

```csharp
// Separar configuración por entidad — nunca todo en OnModelCreating
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL
            // .HasDefaultValueSql("NEWSEQUENTIALID()") // SQL Server

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256)
            .HasConversion(
                email => email.Value,                    // Value Object → DB
                value => new Email(value));              // DB → Value Object (usar ctor interno, no factory)
        // ⚠️ No usar Email.Create().Value en conversiones EF — puede lanzar si el dato en DB es inválido

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        // Owned entity (value object sin tabla propia)
        builder.OwnsOne(u => u.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(300);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.ZipCode).HasMaxLength(10);
        });

        // JSON column (.NET 8)
        builder.OwnsMany(u => u.Tags, tags => tags.ToJson());
    }
}
```

## Columnas JSON nativas (.NET 8)

```csharp
// Mapear propiedades complejas a columna JSON directamente
public class Product
{
    public Guid Id { get; set; }
    public List<ProductAttribute> Attributes { get; set; } = [];
    public ProductMetadata Metadata { get; set; } = new();
}

// En configuración:
builder.OwnsMany(p => p.Attributes, a => a.ToJson());
builder.OwnsOne(p => p.Metadata, m => m.ToJson());

// Query sobre JSON (traducido a SQL):
var products = await context.Products
    .Where(p => p.Attributes.Any(a => a.Name == "Color" && a.Value == "Red"))
    .ToListAsync(ct);
```

## Repositorio — patrón correcto

```csharp
// Interface específica por aggregate, no genérica
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<PagedResult<User>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    void Update(User user);
    void Delete(User user);
}

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Users
            .AsNoTracking()  // ✅ Solo lectura = siempre AsNoTracking
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<PagedResult<User>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Users.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<User>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await context.Users.AddAsync(user, ct);

    // Update y Delete: EF rastrea el objeto, no necesitan async
    public void Update(User user) => context.Users.Update(user);
    public void Delete(User user) => context.Users.Remove(user);
}
```

## Unit of Work

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// AppDbContext ya es el UoW — hacer que implemente la interfaz
public class AppDbContext : DbContext, IUnitOfWork
{
    // SaveChangesAsync ya está implementado
}

// Registro correcto de lifetimes
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention()  // Npgsql.EntityFrameworkCore
           .EnableSensitiveDataLogging(isDevelopment)
           .EnableDetailedErrors(isDevelopment));

services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
```

## Queries eficientes

```csharp
// ✅ Proyecciones — nunca traer entidad completa si solo necesitas campos
var userEmails = await context.Users
    .AsNoTracking()
    .Where(u => u.IsActive)
    .Select(u => new { u.Id, u.Email })  // Solo los campos necesarios
    .ToListAsync(ct);

// ✅ Split query para evitar producto cartesiano con colecciones
var orders = await context.Orders
    .Include(o => o.Items)
    .Include(o => o.Tags)
    .AsSplitQuery()  // 3 queries SQL en lugar de JOIN con duplicados
    .ToListAsync(ct);

// ✅ Compiled queries para queries frecuentes y críticos
private static readonly Func<AppDbContext, string, Task<User?>> GetByEmailQuery =
    EF.CompileAsyncQuery((AppDbContext ctx, string email) =>
        ctx.Users.FirstOrDefault(u => u.Email == email));

// ✅ ExecuteUpdate / ExecuteDelete (.NET 7+) — sin cargar entidades
await context.Users
    .Where(u => u.LastLoginAt < DateTime.UtcNow.AddYears(-1))
    .ExecuteDeleteAsync(ct);

await context.Users
    .Where(u => u.Id == userId)
    .ExecuteUpdateAsync(s => s
        .SetProperty(u => u.LastLoginAt, DateTime.UtcNow)
        .SetProperty(u => u.LoginCount, u => u.LoginCount + 1), ct);

// ✅ Raw SQL cuando es necesario — siempre parametrizado
var users = await context.Users
    .FromSqlInterpolated($"SELECT * FROM users WHERE email = {email}")
    .ToListAsync(ct);
```

## Migraciones — buenas prácticas

```csharp
// Extension method para aplicar migraciones en startup (solo dev/staging)
public static class DatabaseExtensions
{
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrated successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error migrating database");
            throw;
        }
    }
}

// Convenciones de migraciones:
// - Una migración por feature/ticket
// - Nombre descriptivo: AddUserEmailIndex, AddOrdersTable
// - Nunca editar migraciones ya aplicadas en producción
// - Usar HasData() solo para datos de seed estáticos
```

## Performance: Interceptors

```csharp
// Interceptor para detectar queries lentas
public class SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger) 
    : DbCommandInterceptor
{
    private const int SlowQueryThresholdMs = 500;

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Duration.TotalMilliseconds > SlowQueryThresholdMs)
        {
            logger.LogWarning(
                "Slow query detected ({Duration}ms): {CommandText}",
                eventData.Duration.TotalMilliseconds,
                command.CommandText);
        }
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}

// Registrar en DbContext
services.AddDbContext<AppDbContext>(options =>
    options.AddInterceptors(new SlowQueryInterceptor(logger)));
```
