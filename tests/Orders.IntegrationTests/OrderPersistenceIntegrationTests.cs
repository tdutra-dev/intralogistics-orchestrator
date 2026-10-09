using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Orders.Infrastructure;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

namespace Orders.IntegrationTests;

public sealed class OrderPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithPassword("P@ssw0rd!2025")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        await SqlServerBootstrapper.EnsureReportingProcedureAsync(dbContext, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Should_persist_order_and_query_read_side()
    {
        var orderId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var order = new CustomerOrder(orderId, "customer-1", "north", "dock-a");
            order.AddLine("SKU-1", 3, 12.5m);
            order.SetIdempotencyKey("idem-001");

            await dbContext.Orders.AddAsync(order);
            await dbContext.SaveChangesAsync();
        }

        var repository = new OrderReadRepository(_container.GetConnectionString());
        var orders = (await repository.GetOrderSummariesAsync(null, null, null, null, 1, 20, CancellationToken.None)).ToList();
        var report = await repository.GetThroughputReportAsync(CancellationToken.None);

        orders.ShouldContain(x => x.Id == orderId);
        report.TotalOrders.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Should_persist_outbox_message_with_order()
    {
        var orderId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var order = new CustomerOrder(orderId, "customer-outbox", "north", "dock-a");
            order.AddLine("SKU-OUTBOX", 1, 5m);

            var integrationEvent = new Contracts.OrderReceived(order.Id, order.CustomerId, DateTime.UtcNow);

            await dbContext.Orders.AddAsync(order);
            await dbContext.OutboxMessages.AddAsync(new IntegrationEventOutboxMessage(
                integrationEvent.EventId,
                typeof(Contracts.OrderReceived).FullName!,
                System.Text.Json.JsonSerializer.Serialize(integrationEvent),
                integrationEvent.UtcTimestamp));
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = CreateDbContext())
        {
            var outboxMessages = await dbContext.OutboxMessages.AsNoTracking().ToListAsync();

            outboxMessages.Any(x => x.EventType == typeof(Contracts.OrderReceived).FullName && x.DispatchedAtUtc == null).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Should_enforce_unique_idempotency_key()
    {
        await using var dbContext = CreateDbContext();

        var order1 = new CustomerOrder(Guid.NewGuid(), "customer-a", "north", "dock-a");
        order1.AddLine("SKU-1", 1, 10m);
        order1.SetIdempotencyKey("idem-dup");

        var order2 = new CustomerOrder(Guid.NewGuid(), "customer-b", "south", "dock-b");
        order2.AddLine("SKU-2", 2, 8m);
        order2.SetIdempotencyKey("idem-dup");

        await dbContext.Orders.AddAsync(order1);
        await dbContext.SaveChangesAsync();

        await dbContext.Orders.AddAsync(order2);
        await Should.ThrowAsync<DbUpdateException>(async () => await dbContext.SaveChangesAsync());
    }

    private OrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        return new OrderDbContext(options);
    }
}
