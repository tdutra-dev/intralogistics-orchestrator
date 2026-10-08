using Orders.Domain;
using Shouldly;
using Xunit;

namespace Orders.UnitTests;

public sealed class PalletLifecycleTests
{
    [Fact]
    public void Happy_path_transitions_reach_dispatched()
    {
        var pallet = new Pallet(Guid.NewGuid(), Guid.NewGuid(), 120m);

        pallet.TransitionTo(PalletState.Building);
        pallet.TransitionTo(PalletState.Built);
        pallet.TransitionTo(PalletState.Wrapped);
        pallet.TransitionTo(PalletState.Labeled);
        pallet.TransitionTo(PalletState.Routed);
        pallet.TransitionTo(PalletState.InTransit);
        pallet.TransitionTo(PalletState.Dispatched);

        pallet.State.ShouldBe(PalletState.Dispatched);
        pallet.Timeline.Count.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(PalletState.Created, PalletState.Labeled)]
    [InlineData(PalletState.Building, PalletState.Routed)]
    [InlineData(PalletState.Built, PalletState.InTransit)]
    [InlineData(PalletState.Dispatched, PalletState.Building)]
    [InlineData(PalletState.Cancelled, PalletState.Building)]
    public void Invalid_transition_throws_invalid_operation_exception(PalletState currentState, PalletState targetState)
    {
        var pallet = new Pallet(Guid.NewGuid(), Guid.NewGuid(), 120m);

        ForceState(pallet, currentState);

        Should.Throw<InvalidOperationException>(() => pallet.TransitionTo(targetState));
    }

    [Fact]
    public void Faulted_pallet_can_retry_building()
    {
        var pallet = new Pallet(Guid.NewGuid(), Guid.NewGuid(), 120m);
        pallet.TransitionTo(PalletState.Building);

        pallet.MarkFaulted();
        pallet.TransitionTo(PalletState.Building);

        pallet.State.ShouldBe(PalletState.Building);
    }

    [Fact]
    public void Active_pallet_can_be_cancelled()
    {
        var pallet = new Pallet(Guid.NewGuid(), Guid.NewGuid(), 120m);
        pallet.TransitionTo(PalletState.Building);

        pallet.Cancel();

        pallet.State.ShouldBe(PalletState.Cancelled);
    }

    [Fact]
    public void Dispatched_pallet_cannot_be_cancelled()
    {
        var pallet = new Pallet(Guid.NewGuid(), Guid.NewGuid(), 120m);
        pallet.TransitionTo(PalletState.Building);
        pallet.TransitionTo(PalletState.Built);
        pallet.TransitionTo(PalletState.Wrapped);
        pallet.TransitionTo(PalletState.Labeled);
        pallet.TransitionTo(PalletState.Routed);
        pallet.TransitionTo(PalletState.InTransit);
        pallet.TransitionTo(PalletState.Dispatched);

        Should.Throw<InvalidOperationException>(() => pallet.Cancel());
    }

    [Fact]
    public void Order_can_be_created_with_valid_lines()
    {
        var order = new CustomerOrder(Guid.NewGuid(), "customer-1", "north", "dock-a");
        order.AddLine("SKU-1", 10, 5m);

        order.IsValid.ShouldBeTrue();
        order.TotalWeight.ShouldBe(50m);
    }

    private static void ForceState(Pallet pallet, PalletState state)
    {
        switch (state)
        {
            case PalletState.Created:
                return;
            case PalletState.Building:
                pallet.TransitionTo(PalletState.Building);
                return;
            case PalletState.Built:
                pallet.TransitionTo(PalletState.Building);
                pallet.TransitionTo(PalletState.Built);
                return;
            case PalletState.Wrapped:
                pallet.TransitionTo(PalletState.Building);
                pallet.TransitionTo(PalletState.Built);
                pallet.TransitionTo(PalletState.Wrapped);
                return;
            case PalletState.Labeled:
                pallet.TransitionTo(PalletState.Building);
                pallet.TransitionTo(PalletState.Built);
                pallet.TransitionTo(PalletState.Wrapped);
                pallet.TransitionTo(PalletState.Labeled);
                return;
            case PalletState.Routed:
                pallet.TransitionTo(PalletState.Building);
                pallet.TransitionTo(PalletState.Built);
                pallet.TransitionTo(PalletState.Wrapped);
                pallet.TransitionTo(PalletState.Labeled);
                pallet.TransitionTo(PalletState.Routed);
                return;
            case PalletState.InTransit:
                pallet.TransitionTo(PalletState.Building);
                pallet.TransitionTo(PalletState.Built);
                pallet.TransitionTo(PalletState.Wrapped);
                pallet.TransitionTo(PalletState.Labeled);
                pallet.TransitionTo(PalletState.Routed);
                pallet.TransitionTo(PalletState.InTransit);
                return;
            case PalletState.Dispatched:
                pallet.TransitionTo(PalletState.Building);
                pallet.TransitionTo(PalletState.Built);
                pallet.TransitionTo(PalletState.Wrapped);
                pallet.TransitionTo(PalletState.Labeled);
                pallet.TransitionTo(PalletState.Routed);
                pallet.TransitionTo(PalletState.InTransit);
                pallet.TransitionTo(PalletState.Dispatched);
                return;
            case PalletState.Faulted:
                pallet.TransitionTo(PalletState.Building);
                pallet.MarkFaulted();
                return;
            case PalletState.Cancelled:
                pallet.Cancel();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }
}
