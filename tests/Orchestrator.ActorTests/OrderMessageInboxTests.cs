using Shouldly;
using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.ActorTests;

public sealed class OrderMessageInboxTests
{
    [Fact]
    public void Inbox_should_ignore_duplicate_event_ids()
    {
        var inbox = new OrderMessageInbox();
        var eventId = Guid.NewGuid();

        inbox.TryRegister(eventId).ShouldBeTrue();
        inbox.TryRegister(eventId).ShouldBeFalse();
    }
}