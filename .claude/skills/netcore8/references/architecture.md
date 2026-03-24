# Clean Architecture + CQRS con MediatR — .NET 8

## Estructura de un Feature (Vertical Slice)

```
Features/Users/
├── Commands/
│   └── CreateUser/
│       ├── CreateUserCommand.cs      # Record con datos de entrada
│       ├── CreateUserHandler.cs      # Lógica de negocio
│       └── CreateUserValidator.cs    # FluentValidation
└── Queries/
    └── GetUser/
        ├── GetUserQuery.cs
        └── GetUserHandler.cs
```

## Command con Result Pattern

```csharp
// Command como record inmutable
public record CreateUserCommand(string Email, string Name, int Age) 
    : IRequest<Result<Guid>>;

// Validator con FluentValidation
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);

        RuleFor(x => x.Age)
            .InclusiveBetween(0, 150);
    }
}

// Handler — contiene TODA la lógica, sin lógica en controllers/endpoints
internal sealed class CreateUserHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateUserHandler> logger) : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateUserCommand command, 
        CancellationToken cancellationToken)
    {
        // 1. Verificar si ya existe
        var existingUser = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existingUser is not null)
            return Result.Fail<Guid>($"Ya existe un usuario con email {command.Email}");

        // 2. Crear entidad (lógica de dominio en la entidad)
        var userResult = User.Create(command.Email, command.Name, command.Age);
        if (userResult.IsFailed)
            return userResult.ToResult<Guid>();

        // 3. Persistir
        await userRepository.AddAsync(userResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Usuario creado: {UserId} - {Email}", userResult.Value.Id, command.Email);

        return Result.Ok(userResult.Value.Id);
    }
}
```

## Query con proyección directa

```csharp
public record GetUserQuery(Guid Id) : IRequest<Result<UserResponse>>;

// Response como record
public record UserResponse(
    Guid Id, 
    string Email, 
    string Name, 
    int Age,
    DateTime CreatedAt);

internal sealed class GetUserHandler(AppDbContext context) 
    : IRequestHandler<GetUserQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(
        GetUserQuery query, 
        CancellationToken cancellationToken)
    {
        // Proyectar directamente a DTO — evitar cargar entidad completa
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == query.Id)
            .Select(u => new UserResponse(u.Id, u.Email, u.Name, u.Age, u.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? Result.Fail<UserResponse>($"Usuario {query.Id} no encontrado")
            : Result.Ok(user);
    }
}
```

## Pipeline Behaviors

```csharp
// 1. Logging behavior — registra entrada/salida de cada handler
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger) 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Iniciando {RequestName}: {@Request}", requestName, request);

        var response = await next();

        logger.LogInformation("Completado {RequestName}", requestName);
        return response;
    }
}

// 2. Validation behavior — valida antes de llegar al handler
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            // ⚠️ Si TResponse es Result<T>, preferir retornar Result.Fail en vez de lanzar excepción
            // Si el pipeline usa GlobalExceptionHandler, lanzar es aceptable también
            throw new ValidationException(failures);
        }

        return await next();
    }
}

// 3. Performance behavior — alerta si el handler tarda demasiado
public class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            logger.LogWarning(
                "Slow request: {RequestName} ({ElapsedMs}ms) {@Request}",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, request);
        }

        return response;
    }
}
```

## DependencyInjection.cs — Capa Application

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Registrar behaviors en orden (primero logging, luego validation, luego performance)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        // Auto-registrar todos los validators en el assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

## Domain Events

```csharp
// Evento de dominio
// IDomainEvent debe extender INotification de MediatR para usar INotificationHandler
public interface IDomainEvent : INotification { }

public record UserCreatedDomainEvent(Guid UserId, string Email) : IDomainEvent;

// En la entidad
public class User : AggregateRoot
{
    private User() { } // EF Core

    public static Result<User> Create(string email, string name, int age)
    {
        var user = new User { /* propiedades */ };
        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id, email));
        return Result.Ok(user);
    }
}

// Handler del evento
internal sealed class SendWelcomeEmailOnUserCreated(IEmailService emailService) 
    : INotificationHandler<UserCreatedDomainEvent>
{
    public async Task Handle(UserCreatedDomainEvent notification, CancellationToken ct) =>
        await emailService.SendWelcomeAsync(notification.Email, ct);
}

// Publicar events en SaveChanges — IPublisher debe inyectarse en el DbContext
// Nota: AppDbContext recibe IPublisher vía primary constructor
public class AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher) 
    : DbContext(options), IUnitOfWork
{
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        var result = await base.SaveChangesAsync(ct);

        // Publicar DESPUÉS del SaveChanges — los eventos son post-commit
        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, ct);

        return result;
    }
}
```

## Result Pattern (FluentResults o custom)

```csharp
// Usar FluentResults NuGet para Result<T>
// dotnet add package FluentResults

// Errores tipados
public static class UserErrors
{
    public static Error NotFound(Guid id) => new($"Usuario {id} no encontrado");
    public static Error EmailAlreadyExists(string email) => new($"Email {email} ya registrado");
    public static Error InvalidAge(int age) => new($"Edad {age} no válida");
}

// Uso en handler
return Result.Fail<Guid>(UserErrors.EmailAlreadyExists(command.Email));

// Mapeo en endpoint — FluentResults no tiene Match, usar IsSuccess
return result.IsSuccess
    ? TypedResults.Created($"/users/{result.Value}", new { Id = result.Value })
    : TypedResults.BadRequest(result.Errors.Select(e => e.Message));
```
