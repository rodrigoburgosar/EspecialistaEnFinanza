# Controllers — ASP.NET Core 8 Best Practices

## Estructura base recomendada

```csharp
// ✅ Usar ApiController + Route en cada controller
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class UsersController(ISender sender) : ControllerBase
{
    // Usar ISender de MediatR — el controller solo despacha, no tiene lógica
    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Errors);
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var result = await sender.Send(request.ToCommand(), ct);
        if (result.IsFailed) return BadRequest(result.Errors);
        return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { Id = result.Value });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        if (id != request.Id) return BadRequest("ID mismatch");
        var result = await sender.Send(request.ToCommand(), ct);
        return result.IsSuccess ? NoContent() : NotFound(result.Errors);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteUserCommand(id), ct);
        return result.IsSuccess ? NoContent() : NotFound(result.Errors);
    }
}
```

## [ApiController] — comportamiento automático

```csharp
// [ApiController] activa automáticamente:
// ✅ Validación de ModelState — responde 400 si model binding falla
// ✅ Infiere [FromBody] en parámetros complejos de POST/PUT
// ✅ Infiere [FromRoute] para parámetros que coinciden con la ruta
// ✅ Respuestas ProblemDetails estándar en errores de binding

// ❌ Nunca verificar ModelState manualmente con [ApiController]
if (!ModelState.IsValid) return BadRequest(ModelState); // innecesario

// ✅ Personalizar respuesta de validación si se necesita
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ExceptionHandlingFilter>();
})
.ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.Id;
        return new BadRequestObjectResult(problemDetails);
    };
});
```

## Action Filters

```csharp
// Filtro de excepción global
public class ExceptionHandlingFilter(ILogger<ExceptionHandlingFilter> logger) 
    : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        logger.LogError(context.Exception, "Unhandled exception in {Action}", 
            context.ActionDescriptor.DisplayName);

        var problemDetails = context.Exception switch
        {
            ValidationException ex => new ValidationProblemDetails(
                ex.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest
            },
            KeyNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = context.Exception.Message
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred"
            }
        };

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
        context.ExceptionHandled = true;
    }
}

// Filtro de auditoría como atributo
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AuditAttribute(string action) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await next();

        if (result.Exception == null)
        {
            // Loguear auditoría
        }
    }
}
```

## Model Binding — casos especiales

```csharp
// Custom model binder para tipos de dominio
public class GuidBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
        if (Guid.TryParse(value, out var guid))
        {
            bindingContext.Result = ModelBindingResult.Success(guid);
        }
        else
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid GUID format");
            bindingContext.Result = ModelBindingResult.Failed();
        }
        return Task.CompletedTask;
    }
}

// Binding desde múltiples fuentes
public class ComplexRequest
{
    [FromRoute] public Guid OrganizationId { get; set; }
    [FromQuery] public string? Filter { get; set; }
    [FromBody] public CreateItemDto Item { get; set; } = null!;
    [FromHeader(Name = "X-Idempotency-Key")] public string? IdempotencyKey { get; set; }
}
```

## Response Caching en Controllers

```csharp
// Cache de respuesta HTTP
[HttpGet("{id:guid}")]
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
public async Task<IActionResult> GetById(Guid id, CancellationToken ct) { ... }

// No cachear
[HttpGet("me")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public async Task<IActionResult> GetCurrentUser(CancellationToken ct) { ... }
```

## Convenciones de respuestas HTTP

```csharp
// Tabla de referencia — qué método retornar en cada situación
// GET encontrado          → Ok(recurso)              200
// GET no encontrado       → NotFound()                404
// POST creado             → CreatedAtAction(...)      201
// PUT actualizado         → NoContent()               204
// DELETE eliminado        → NoContent()               204
// Validación fallida      → BadRequest(errors)        400
// No autenticado          → Unauthorized()            401
// Sin permisos            → Forbid()                  403
// Conflicto (duplicado)   → Conflict(detail)          409
// Error interno           → manejado por filtro       500

// ✅ Usar CreatedAtAction para 201 — incluye Location header
return CreatedAtAction(
    nameof(GetById),
    new { id = result.Value },
    new { Id = result.Value });
```
