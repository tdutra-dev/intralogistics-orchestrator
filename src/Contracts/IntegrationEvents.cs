namespace Contracts;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime UtcTimestamp { get; }
}

public sealed record OrderReceived(Guid OrderId, string CustomerId, DateTime UtcTimestamp) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record PalletCreated(Guid PalletId, Guid OrderId, DateTime UtcTimestamp) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record PalletDispatched(Guid PalletId, Guid OrderId, DateTime UtcTimestamp) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record MachineFaulted(Guid MachineId, string FaultCode, DateTime UtcTimestamp) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record MachineRecovered(Guid MachineId, DateTime UtcTimestamp) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record MachineTelemetryReceived(
    Guid MachineId,
    long Sequence,
    string Status,
    decimal TemperatureC,
    long CycleCount,
    string? FaultCode,
    DateTime UtcTimestamp) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
