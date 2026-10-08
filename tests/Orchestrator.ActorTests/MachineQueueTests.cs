using Orchestrator.Domain;
using Shouldly;
using Xunit;

namespace Orchestrator.ActorTests;

public sealed class MachineQueueTests
{
    [Fact]
    public void Enqueue_and_dequeue_keep_fifo_order()
    {
        var queue = new MachineQueue(3);

        queue.TryEnqueue(Guid.Parse("11111111-1111-1111-1111-111111111111")).ShouldBeTrue();
        queue.TryEnqueue(Guid.Parse("22222222-2222-2222-2222-222222222222")).ShouldBeTrue();

        queue.Dequeue().ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        queue.Dequeue().ShouldBe(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }

    [Fact]
    public void Queue_rejects_when_full()
    {
        var queue = new MachineQueue(1);
        queue.TryEnqueue(Guid.NewGuid()).ShouldBeTrue();
        queue.TryEnqueue(Guid.NewGuid()).ShouldBeFalse();
    }
}
