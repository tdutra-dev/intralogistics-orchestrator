using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddSingleton<OrderApplicationService>();
builder.Services.AddScoped<OrderReadRepository>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is required.");
    return new OrderReadRepository(connectionString);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.MapPost("/api/v1/orders", async Task<IResult>
    ([FromBody] CreateOrderRequest request, HttpContext httpContext, OrderApplicationService service, OrderDbContext dbContext, CancellationToken cancellationToken) =>
{
    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var existing = await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return TypedResults.Conflict(new { message = "Duplicate Idempotency-Key.", existingOrderId = existing.Id });
        }
    }

    var result = service.CreateOrder(
        request.CustomerId,
        request.DestinationRegion,
        request.DockPreference,
        request.Lines.Select(x => new CustomerOrderLine(x.Sku, x.Quantity, x.WeightKg)).ToArray());

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new { error = result.Error });
    }

    var order = result.Value!;
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        order.SetIdempotencyKey(idempotencyKey);
    }

    await dbContext.Orders.AddAsync(order, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    return TypedResults.Created($"/api/v1/orders/{order.Id}", new OrderResponse(order.Id, order.CustomerId, order.Status.ToString(), order.Priority.ToString()));
});

app.MapGet("/api/v1/orders/{id:guid}", async Task<Results<Ok<OrderDetailsResponse>, NotFound>>
    (Guid id, OrderDbContext dbContext, CancellationToken cancellationToken) =>
{
    var order = await dbContext.Orders
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (order is null)
    {
        return TypedResults.NotFound();
    }

    var pallets = await dbContext.Pallets
        .AsNoTracking()
        .Where(x => x.OrderId == order.Id)
        .Select(x => new PalletResponse(x.Id, x.State.ToString(), x.WeightKg, x.AssignedRoute, x.Timeline.ToArray()))
        .ToArrayAsync(cancellationToken);

    return TypedResults.Ok(new OrderDetailsResponse(
        order.Id,
        order.CustomerId,
        order.DestinationRegion,
        order.DockPreference,
        order.Priority.ToString(),
        order.Status.ToString(),
        order.Lines,
        pallets));
});

app.MapGet("/api/v1/orders", async Task<Ok<IEnumerable<OrderListItem>>>
    ([FromQuery] string? status, [FromQuery] string? priority, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int page, [FromQuery] int pageSize,
        OrderReadRepository repository, CancellationToken cancellationToken) =>
{
    var normalizedPage = page <= 0 ? 1 : page;
    var normalizedPageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 100);

    var result = await repository.GetOrderSummariesAsync(
        status,
        priority,
        from,
        to,
        normalizedPage,
        normalizedPageSize,
        cancellationToken);

    return TypedResults.Ok(result);
});

app.MapPost("/api/v1/orders/{id:guid}/cancel", async Task<Results<NoContent, NotFound>>
    (Guid id, OrderDbContext dbContext, CancellationToken cancellationToken) =>
{
    var order = await dbContext.Orders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (order is null)
    {
        return TypedResults.NotFound();
    }

    order.Cancel();
    await dbContext.SaveChangesAsync(cancellationToken);
    return TypedResults.NoContent();
});

app.MapGet("/api/v1/pallets/{id:guid}/timeline", async Task<Results<Ok<IEnumerable<string>>, NotFound>>
    (Guid id, OrderDbContext dbContext, CancellationToken cancellationToken) =>
{
    var pallet = await dbContext.Pallets
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (pallet is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(pallet.Timeline.AsEnumerable());
});

app.MapGet("/api/v1/reports/throughput", async Task<Ok<ThroughputReportResponse>>
    (OrderReadRepository repository, CancellationToken cancellationToken) =>
{
    var report = await repository.GetThroughputReportAsync(cancellationToken);
    return TypedResults.Ok(report);
});

app.Run();

public sealed record CreateOrderRequest(string CustomerId, string DestinationRegion, string DockPreference, List<CreateOrderLineRequest> Lines);
public sealed record CreateOrderLineRequest(string Sku, int Quantity, decimal WeightKg);
public sealed record OrderResponse(Guid Id, string CustomerId, string Status, string Priority);
public sealed record OrderDetailsResponse(
    Guid Id,
    string CustomerId,
    string DestinationRegion,
    string DockPreference,
    string Priority,
    string Status,
    IReadOnlyCollection<CustomerOrderLine> Lines,
    IReadOnlyCollection<PalletResponse> Pallets);

public sealed record PalletResponse(Guid Id, string State, decimal WeightKg, string? AssignedRoute, IReadOnlyCollection<string> Timeline);
