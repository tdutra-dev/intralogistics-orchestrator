namespace Orders.Domain;

public enum PalletState
{
    Created,
    Building,
    Built,
    Wrapped,
    Labeled,
    Routed,
    InTransit,
    Dispatched,
    Faulted,
    Cancelled
}

public sealed class Pallet
{
    private readonly List<string> _timeline = new();

    private Pallet()
    {
        Id = Guid.Empty;
        OrderId = Guid.Empty;
        WeightKg = 0m;
        State = PalletState.Created;
    }

    public Pallet(Guid id, Guid orderId, decimal weightKg)
    {
        Id = id;
        OrderId = orderId;
        WeightKg = weightKg;
        State = PalletState.Created;
        _timeline.Add($"{DateTime.UtcNow:O}:{State}");
    }

    public Guid Id { get; }
    public Guid OrderId { get; }
    public decimal WeightKg { get; }
    public PalletState State { get; private set; }
    public string? AssignedRoute { get; private set; }
    public IReadOnlyList<string> Timeline => _timeline;

    public void TransitionTo(PalletState nextState)
    {
        if (!CanTransition(State, nextState))
        {
            throw new InvalidOperationException($"Invalid pallet transition from {State} to {nextState}.");
        }

        State = nextState;
        _timeline.Add($"{DateTime.UtcNow:O}:{State}");
    }

    public void SetRoute(string route) => AssignedRoute = route;
    public void MarkFaulted() => TransitionTo(PalletState.Faulted);
    public void Cancel() => TransitionTo(PalletState.Cancelled);

    private static bool CanTransition(PalletState current, PalletState next)
    {
        if (next == PalletState.Cancelled)
        {
            return current is not PalletState.Cancelled and not PalletState.Dispatched;
        }

        if (next == PalletState.Faulted)
        {
            return current is not PalletState.Cancelled and not PalletState.Dispatched and not PalletState.Faulted;
        }

        return current switch
        {
            PalletState.Created when next == PalletState.Building => true,
            PalletState.Building when next == PalletState.Built => true,
            PalletState.Built when next == PalletState.Wrapped => true,
            PalletState.Wrapped when next == PalletState.Labeled => true,
            PalletState.Labeled when next == PalletState.Routed => true,
            PalletState.Routed when next == PalletState.InTransit => true,
            PalletState.InTransit when next == PalletState.Dispatched => true,
            PalletState.Faulted when next == PalletState.Building || next == PalletState.Cancelled => true,
            PalletState.Cancelled => false,
            _ => false
        };
    }
}
