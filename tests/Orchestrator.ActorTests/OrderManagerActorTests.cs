using Akka.Actor;
using Akka.TestKit.Xunit2;
using Orchestrator.Domain;
using Shouldly;
using Xunit;

namespace Orchestrator.ActorTests;

public sealed class OrderManagerActorTests : TestKit
{
    [Fact]
    public void Queue_should_apply_backpressure_when_full()
    {
        var actor = Sys.ActorOf(Props.Create(() => new OrderManagerActor(1)));
        var firstOrder = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondOrder = Guid.Parse("22222222-2222-2222-2222-222222222222");

        actor.Tell(new OrderReceivedMessage(firstOrder, "customer-1"), TestActor);
        ExpectMsg<OrderQueuedMessage>(message => message.OrderId == firstOrder && message.QueueDepth == 1);

        actor.Tell(new OrderReceivedMessage(secondOrder, "customer-2"), TestActor);
        ExpectMsg<OrderBackpressuredMessage>(message => message.OrderId == secondOrder && message.QueueDepth == 1);
    }

    [Fact]
    public void Queue_should_restart_after_fault_and_lose_in_memory_state()
    {
        var actor = Sys.ActorOf(Props.Create(() => new OrderManagerActor(2)));
        var orderId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        actor.Tell(new OrderReceivedMessage(orderId, "customer-3"), TestActor);
        ExpectMsg<OrderQueuedMessage>(message => message.OrderId == orderId && message.QueueDepth == 1);

        actor.Tell(new MachineFaultMessage(Guid.NewGuid(), "QUEUE-FAULT"));

        AwaitAssert(() =>
        {
            var depth = actor.Ask<QueueDepthMessage>(new QueryQueueDepthMessage(), TimeSpan.FromSeconds(2)).Result;
            depth.QueueDepth.ShouldBe(0);
        });
    }
}