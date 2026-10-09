namespace Orchestrator.Domain;

public sealed record OrderReceivedMessage(Guid OrderId, string CustomerId);
public sealed record MachineFaultMessage(Guid MachineId, string FaultCode);
public sealed record MachineRecoveryMessage(Guid MachineId);
public sealed record RouteBlockedMessage(string NodeId);
public sealed record QueueOrderMessage(Guid OrderId);
public sealed record OrderQueuedMessage(Guid OrderId, int QueueDepth);
public sealed record OrderBackpressuredMessage(Guid OrderId, int QueueDepth);
public sealed record QueryQueueDepthMessage;
public sealed record QueueDepthMessage(int QueueDepth);

public sealed class OrderMessageInbox
{
	private readonly HashSet<Guid> _processedMessages = new();

	public bool TryRegister(Guid eventId)
	{
		return _processedMessages.Add(eventId);
	}
}
