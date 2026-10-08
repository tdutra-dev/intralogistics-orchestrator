namespace Orchestrator.Domain;

public sealed class MachineQueue
{
    private readonly Queue<Guid> _pending = new();
    private readonly int _maxQueueDepth;

    public MachineQueue(int maxQueueDepth = 10)
    {
        _maxQueueDepth = maxQueueDepth;
    }

    public bool TryEnqueue(Guid palletId)
    {
        if (_pending.Count >= _maxQueueDepth)
        {
            return false;
        }

        _pending.Enqueue(palletId);
        return true;
    }

    public Guid? Dequeue()
    {
        if (_pending.Count == 0)
        {
            return null;
        }

        return _pending.Dequeue();
    }

    public int Count => _pending.Count;
}
