# Messaging — MassTransit + Outbox Pattern + Eventos de Integración

## MassTransit con RabbitMQ / Azure Service Bus

```csharp
// dotnet add package MassTransit.RabbitMQ
// dotnet add package MassTransit.EntityFrameworkCore (para Outbox)

// Program.cs
builder.Services.AddMassTransit(cfg =>
{
    // Auto-registrar todos los consumers en el assembly
    cfg.AddConsumers(typeof(Program).Assembly);

    // Outbox pattern — garantía at-least-once
    cfg.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    cfg.UsingRabbitMq((context, rmq) =>
    {
        rmq.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        rmq.ConfigureEndpoints(context);

        // Política de retry para consumers
        rmq.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
    });
});
```

## Definir mensajes como records inmutables

```csharp
// Contratos de mensajes en proyecto compartido o en Application layer
// Eventos de integración — lo que ocurrió (pasado)
public record UserCreatedIntegrationEvent(
    Guid UserId,
    string Email,
    string Name,
    DateTime OccurredAt) : IntegrationEvent;

// Comandos de integración — lo que debe ocurrir (imperativo)
public record SendWelcomeEmailCommand(
    Guid UserId,
    string Email,
    string Name) : IntegrationCommand;

// Base classes
public abstract record IntegrationEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

## Publisher — publicar desde el Handler

```csharp
// En el handler DESPUÉS de SaveChanges — el Outbox garantiza entrega
internal sealed class CreateUserHandler(
    IUserRepository repository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint) : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken ct)
    {
        var user = User.Create(command.Email, command.Name);
        await repository.AddAsync(user.Value, ct);

        // El Outbox guarda el evento en la MISMA transacción que el usuario
        await publishEndpoint.Publish(
            new UserCreatedIntegrationEvent(user.Value.Id, command.Email, command.Name, DateTime.UtcNow),
            ct);

        await unitOfWork.SaveChangesAsync(ct); // commit ambos en la misma TX

        return Result.Ok(user.Value.Id);
    }
}
```

## Consumer

```csharp
// Consumer — procesa mensajes entrantes
public class UserCreatedConsumer(IEmailService emailService, ILogger<UserCreatedConsumer> logger)
    : IConsumer<UserCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserCreatedIntegrationEvent> context)
    {
        var evt = context.Message;
        logger.LogInformation("Procesando UserCreated para {UserId}", evt.UserId);

        await emailService.SendWelcomeEmailAsync(evt.Email, evt.Name, context.CancellationToken);
    }
}

// Consumer con manejo de faults — mensajes que fallan repetidamente
public class UserCreatedConsumerFault : IConsumer<Fault<UserCreatedIntegrationEvent>>
{
    public async Task Consume(ConsumeContext<Fault<UserCreatedIntegrationEvent>> context)
    {
        // Manejar el mensaje que falló después de todos los reintentos
        // Loguear, alertar, mover a dead letter queue, etc.
    }
}
```

## Sagas — coordinación de procesos distribuidos

```csharp
// State machine saga para proceso de onboarding
public class OnboardingStateMachine : MassTransitStateMachine<OnboardingState>
{
    public State Registered { get; set; } = null!;
    public State EmailVerified { get; set; } = null!;
    public State Completed { get; set; } = null!;

    public Event<UserCreatedIntegrationEvent> UserCreated { get; set; } = null!;
    public Event<EmailVerifiedEvent> EmailVerified_Event { get; set; } = null!;
    public Schedule<OnboardingState, OnboardingTimeout> OnboardingTimeout { get; set; } = null!;

    public OnboardingStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => UserCreated, x => x.CorrelateById(m => m.Message.UserId));
        Event(() => EmailVerified_Event, x => x.CorrelateById(m => m.Message.UserId));

        Schedule(() => OnboardingTimeout, x => x.TimeoutTokenId,
            s => s.Delay = TimeSpan.FromDays(3));

        Initially(
            When(UserCreated)
                .Then(ctx => ctx.Saga.Email = ctx.Message.Email)
                .Schedule(OnboardingTimeout, ctx => new OnboardingTimeout { UserId = ctx.Saga.CorrelationId })
                .TransitionTo(Registered));

        During(Registered,
            When(EmailVerified_Event)
                .Unschedule(OnboardingTimeout)
                .TransitionTo(EmailVerified));

        During(Registered,
            When(OnboardingTimeout.Received)
                .Publish(ctx => new SendReminderEmailCommand(ctx.Saga.Email))
                .Finalize());
    }
}

public class OnboardingState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Guid? TimeoutTokenId { get; set; }
}
```

## Outbox Pattern manual (sin MassTransit)

```csharp
// Para escenarios simples sin message broker completo
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

// Worker que procesa el outbox
public class OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingMessagesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type)!;
                var payload = JsonSerializer.Deserialize(message.Payload, eventType)!;
                await publisher.Publish(payload, eventType, ct);
                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error procesando mensaje outbox {MessageId}", message.Id);
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
```
