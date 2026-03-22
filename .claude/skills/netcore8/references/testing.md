# Testing — .NET 8 Best Practices

## Estructura y convenciones

```
tests/
├── MyApp.UnitTests/
│   ├── Features/Users/
│   │   ├── CreateUserHandlerTests.cs
│   │   └── GetUserHandlerTests.cs
│   └── Domain/
│       └── UserTests.cs
├── MyApp.IntegrationTests/
│   ├── Users/
│   │   └── UsersEndpointTests.cs
│   └── Infrastructure/
│       ├── CustomWebApplicationFactory.cs
│       └── DatabaseFixture.cs
└── MyApp.ArchitectureTests/
    └── LayerDependencyTests.cs
```

## Unit Tests con xUnit + FluentAssertions + NSubstitute

```csharp
// ✅ Naming: Método_Escenario_ResultadoEsperado
public class CreateUserHandlerTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CreateUserHandler> _logger = Substitute.For<ILogger<CreateUserHandler>>();

    private CreateUserHandler CreateHandler() => 
        new(_repository, _unitOfWork, _logger);

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessWithUserId()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User", 25);
        _repository.GetByEmailAsync(command.Email, default)
            .Returns((User?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _repository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsFailure()
    {
        // Arrange
        var existingUser = UserFactory.Create();
        var command = new CreateUserCommand(existingUser.Email, "Other User", 25);
        _repository.GetByEmailAsync(command.Email, default)
            .Returns(existingUser);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains(command.Email));
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    // Theory para múltiples casos
    [Theory]
    [InlineData("", "Valid Name", 25)]          // email vacío
    [InlineData("not-an-email", "Valid", 25)]   // email inválido
    [InlineData("valid@test.com", "", 25)]       // nombre vacío
    [InlineData("valid@test.com", "Valid", -1)] // edad negativa
    public async Task Handle_WithInvalidCommand_ReturnsValidationError(
        string email, string name, int age)
    {
        var validator = new CreateUserCommandValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand(email, name, age));
        result.IsValid.Should().BeFalse();
    }
}
```

## Factory de objetos de prueba

```csharp
// Usar Bogus para datos realistas
public static class UserFactory
{
    private static readonly Faker<User> _faker = new Faker<User>()
        .CustomInstantiator(f => User.Create(
            f.Internet.Email(),
            f.Name.FullName(),
            f.Random.Int(18, 80)).Value);

    public static User Create() => _faker.Generate();
    public static List<User> CreateMany(int count = 5) => _faker.Generate(count);
    
    public static User CreateWithEmail(string email)
    {
        var user = Create();
        // Usar reflection o método específico si email es inmutable
        return user;
    }
}
```

## Integration Tests con WebApplicationFactory

```csharp
// Factory personalizada con DB en memoria
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Reemplazar DbContext con en memoria
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}"));

            // Reemplazar servicios externos con mocks
            services.AddScoped<IEmailService, FakeEmailService>();
        });

        builder.UseEnvironment("Testing");
    }
}

// Test de integración de endpoint
public class UsersEndpointTests(CustomWebApplicationFactory factory) 
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task POST_Users_WithValidRequest_Returns201()
    {
        // Arrange
        var request = new CreateUserRequest("test@example.com", "Test User", 25);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateUserResponse>();
        result!.Id.Should().NotBeEmpty();
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_Users_WithInvalidEmail_Returns400WithProblemDetails()
    {
        // Arrange
        var request = new CreateUserRequest("not-an-email", "Test", 25);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Email");
    }
}
```

## Integration Tests con DB real (TestContainers)

```csharp
// dotnet add package Testcontainers.PostgreSql

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Aplicar migraciones
        using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.StopAsync();

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);
}
```

## Architecture Tests con NetArchTest

```csharp
// dotnet add package NetArchTest.Rules

public class LayerDependencyTests
{
    [Fact]
    public void Domain_ShouldNot_DependOn_Application()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .Should()
            .NotHaveDependencyOn("MyApp.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(CreateUserHandler).Assembly)
            .Should()
            .NotHaveDependencyOn("MyApp.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_Should_BeSealed()
    {
        var result = Types.InAssembly(typeof(CreateUserHandler).Assembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```
