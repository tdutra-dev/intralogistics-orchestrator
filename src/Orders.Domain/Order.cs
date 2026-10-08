namespace Orders.Domain;

public enum OrderPriority
{
    Low,
    Normal,
    Urgent
}

public enum OrderStatus
{
    Received,
    Building,
    Ready,
    Cancelled,
    Dispatched
}

public sealed record CustomerOrderLine(string Sku, int Quantity, decimal WeightKg);

public sealed class CustomerOrder
{
    private readonly List<CustomerOrderLine> _lines = new();

    private CustomerOrder()
    {
        Id = Guid.Empty;
        CustomerId = string.Empty;
        DestinationRegion = string.Empty;
        DockPreference = string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public CustomerOrder(Guid id, string customerId, string destinationRegion, string dockPreference)
    {
        Id = id;
        CustomerId = customerId;
        DestinationRegion = destinationRegion;
        DockPreference = dockPreference;
        Status = OrderStatus.Received;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; }
    public string CustomerId { get; }
    public string DestinationRegion { get; }
    public string DockPreference { get; }
    public string? IdempotencyKey { get; private set; }
    public OrderPriority Priority { get; private set; } = OrderPriority.Normal;
    public bool HazardousFlag { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public IReadOnlyCollection<CustomerOrderLine> Lines => _lines;

    public void AddLine(string sku, int quantity, decimal weightKg)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (weightKg <= 0m) throw new ArgumentOutOfRangeException(nameof(weightKg));

        _lines.Add(new CustomerOrderLine(sku, quantity, weightKg));
    }

    public void SetPriority(OrderPriority priority) => Priority = priority;
    public void SetIdempotencyKey(string idempotencyKey) => IdempotencyKey = idempotencyKey;
    public void MarkHazardous() => HazardousFlag = true;
    public void Cancel() => Status = OrderStatus.Cancelled;
    public void MarkReady() => Status = OrderStatus.Ready;
    public void MarkDispatched() => Status = OrderStatus.Dispatched;

    public decimal TotalWeight => _lines.Sum(x => x.WeightKg * x.Quantity);
    public bool IsValid => _lines.Count > 0 && TotalWeight > 0m;
}
