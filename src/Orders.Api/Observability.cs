using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class OrdersApiTelemetry
{
    public const string MeterName = "Intralogistics.Orders.Api";
    public const string ActivitySourceName = "Intralogistics.Orders.Api";

    public static readonly Meter Meter = new(MeterName);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Counter<long> OrdersCreated = Meter.CreateCounter<long>("orders.created");
    public static readonly Counter<long> OutboxMessagesStaged = Meter.CreateCounter<long>("orders.outbox.staged");
    public static readonly Counter<long> OutboxMessagesPublished = Meter.CreateCounter<long>("orders.outbox.published");
}