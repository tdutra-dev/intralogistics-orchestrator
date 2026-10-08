namespace Orders.Domain;

public enum MachineType
{
    Palletizer,
    Wrapper,
    Labeler,
    Shuttle
}

public enum MachineStatus
{
    Idle,
    Busy,
    Faulted,
    Maintenance
}

public sealed class Machine
{
    public Machine(Guid id, MachineType type)
    {
        Id = id;
        Type = type;
        Status = MachineStatus.Idle;
    }

    public Guid Id { get; }
    public MachineType Type { get; }
    public MachineStatus Status { get; private set; }

    public void MarkBusy() => Status = MachineStatus.Busy;
    public void MarkIdle() => Status = MachineStatus.Idle;
    public void MarkFaulted() => Status = MachineStatus.Faulted;
    public void MarkMaintenance() => Status = MachineStatus.Maintenance;
}
