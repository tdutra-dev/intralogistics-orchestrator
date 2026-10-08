using Orders.Domain;
using Shouldly;
using Xunit;

namespace Orders.UnitTests;

public sealed class PalletLifecycleTests
{
    private static readonly Dictionary<PalletState, HashSet<PalletState>> AllowedTransitions = new()
    {
        [PalletState.Created] = new HashSet<PalletState> { PalletState.Building, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.Building] = new HashSet<PalletState> { PalletState.Built, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.Built] = new HashSet<PalletState> { PalletState.Wrapped, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.Wrapped] = new HashSet<PalletState> { PalletState.Labeled, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.Labeled] = new HashSet<PalletState> { PalletState.Routed, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.Routed] = new HashSet<PalletState> { PalletState.InTransit, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.InTransit] = new HashSet<PalletState> { PalletState.Dispatched, PalletState.Faulted, PalletState.Cancelled },
        [PalletState.Faulted] = new HashSet<PalletState> { PalletState.Building, PalletState.Cancelled },
        [PalletState.Cancelled] = new HashSet<PalletState>(),
        [PalletState.Dispatched] = new HashSet<PalletState>()
    };

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

    [Fact]
    public void Transition_matrix_should_match_domain_rules()
    {
        var allStates = Enum.GetValues<PalletState>();

        foreach (var current in allStates)
        {
            foreach (var next in allStates)
            {
                if (current == next)
                {
                    continue;
                }

                var pallet = new Pallet(Guid.NewGuid(), Guid.NewGuid(), 120m);
                ForceState(pallet, current);

                var shouldBeAllowed = AllowedTransitions[current].Contains(next);
                if (shouldBeAllowed)
                {
                    pallet.TransitionTo(next);
                    pallet.State.ShouldBe(next);
                }
                else
                {
                    Should.Throw<InvalidOperationException>(() => pallet.TransitionTo(next));
                }
            }
        }
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
