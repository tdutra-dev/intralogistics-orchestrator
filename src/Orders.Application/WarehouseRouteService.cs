using BuildingBlocks;
using NRules.Fluent;
using NRules.Fluent.Dsl;
using Orders.Domain;
using NRules;
using System.Collections.Generic;
using System.Linq;

namespace Orders.Application;

public sealed class WarehouseRouteService
{
    private static readonly Lazy<ISessionFactory> SessionFactory = new(() =>
    {
        var repository = new RuleRepository();
        repository.Load(x => x.From(typeof(RoutePlanRule).Assembly));
        return repository.Compile();
    });

    public Result<List<string>> GetRoute(WarehouseGraph graph, string startNode, string endNode, string? blockedNode = null)
    {
        if (graph is null) return Result<List<string>>.Failure("Graph is required.");

        var scenario = new RouteScenario(graph, startNode, endNode, blockedNode);
        var plan = new RoutePlan();

        var session = SessionFactory.Value.CreateSession();
        session.Insert(scenario);
        session.Insert(plan);
        session.Fire();

        return plan.Route is null
            ? Result<List<string>>.Failure(plan.Reason ?? "No route available.")
            : Result<List<string>>.Success(plan.Route);
    }
}

public sealed record RouteScenario(WarehouseGraph Graph, string StartNode, string EndNode, string? BlockedNode);

public sealed class RoutePlan
{
    public List<string>? Route { get; private set; }
    public string? Reason { get; private set; }

    public void SetRoute(List<string> route)
    {
        Route = route;
        Reason = null;
    }

    public void SetFailure(string reason)
    {
        Route = null;
        Reason = reason;
    }
}

public static class RoutePlanningLogic
{
    public static void ApplyBaseRoute(RouteScenario scenario, RoutePlan plan)
    {
        var path = scenario.Graph.FindShortestPath(scenario.StartNode, scenario.EndNode);
        if (path.IsSuccess)
        {
            plan.SetRoute(path.Value!);
        }
        else
        {
            plan.SetFailure(path.Error ?? "No route available.");
        }
    }

    public static void ApplyBlockedRoute(RouteScenario scenario, RoutePlan plan)
    {
        if (!string.IsNullOrWhiteSpace(scenario.BlockedNode))
        {
            scenario.Graph.BlockNode(scenario.BlockedNode);
        }

        ApplyBaseRoute(scenario, plan);
    }
}

public sealed class RoutePlanRule : Rule
{
    public override void Define()
    {
        RouteScenario scenario = default!;
        RoutePlan plan = default!;

        When()
            .Match(() => scenario, s => string.IsNullOrWhiteSpace(s.BlockedNode))
            .Match(() => plan, p => p.Route == null);

        Then()
            .Do(_ => RoutePlanningLogic.ApplyBaseRoute(scenario, plan));
    }
}

public sealed class BlockedRoutePlanRule : Rule
{
    public override void Define()
    {
        RouteScenario scenario = default!;
        RoutePlan plan = default!;

        When()
            .Match(() => scenario, s => !string.IsNullOrWhiteSpace(s.BlockedNode))
            .Match(() => plan, p => p.Route == null);

        Then()
            .Do(_ => RoutePlanningLogic.ApplyBlockedRoute(scenario, plan));
    }
}
