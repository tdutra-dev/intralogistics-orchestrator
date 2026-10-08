namespace Orchestrator.Domain;

public sealed record OrderReceivedMessage(Guid OrderId, string CustomerId);
public sealed record MachineFaultMessage(Guid MachineId, string FaultCode);
public sealed record MachineRecoveryMessage(Guid MachineId);
public sealed record RouteBlockedMessage(string NodeId);
