using Orders.Application;
using Orders.Domain;
using Shouldly;
using Xunit;

namespace Orders.UnitTests;

public sealed class WarehouseRouteServiceTests
{
    private readonly WarehouseRouteService _service = new();

    [Fact]
    public void GetRoute_should_choose_shortest_path_when_unblocked()
    {
        var graph = BuildSampleGraph();

        var result = _service.GetRoute(graph, "A", "D");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBe(new List<string> { "A", "B", "D" });
    }

    [Fact]
    public void GetRoute_should_reroute_when_middle_node_blocked()
    {
        var graph = BuildSampleGraph();

        var result = _service.GetRoute(graph, "A", "D", "B");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldNotContain("B");
        result.Value.ShouldBe(new List<string> { "A", "C", "D" });
    }

    [Fact]
    public void GetRoute_should_fail_when_no_path_exists()
    {
        var graph = new WarehouseGraph();
        graph.AddNode("A", "dock");
        graph.AddNode("B", "lane");

        var result = _service.GetRoute(graph, "A", "B");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("No route available.");
    }

    [Fact]
    public void Blocked_nodes_should_never_appear_in_route_across_small_graphs()
    {
        var random = new Random(20261009);

        for (var iteration = 0; iteration < 10; iteration++)
        {
            var graph = BuildRandomGraph(random);
            var blockedNode = iteration % 2 == 0 ? "B" : "C";

            var result = _service.GetRoute(graph, "A", "D", blockedNode);

            if (result.IsSuccess)
            {
                result.Value.ShouldNotBeNull();
                result.Value.ShouldNotContain(blockedNode);
                result.Value.First().ShouldBe("A");
                result.Value.Last().ShouldBe("D");
            }
        }
    }

    private static WarehouseGraph BuildSampleGraph()
    {
        var graph = new WarehouseGraph();
        graph.AddNode("A", "dock");
        graph.AddNode("B", "lane");
        graph.AddNode("C", "lane");
        graph.AddNode("D", "dispatch");

        graph.AddEdge("A", "B", 1m);
        graph.AddEdge("B", "D", 1m);
        graph.AddEdge("A", "C", 2m);
        graph.AddEdge("C", "D", 1m);

        return graph;
    }

    private static WarehouseGraph BuildRandomGraph(Random random)
    {
        var graph = new WarehouseGraph();
        graph.AddNode("A", "dock");
        graph.AddNode("B", "lane");
        graph.AddNode("C", "lane");
        graph.AddNode("D", "dispatch");

        graph.AddEdge("A", "B", random.Next(1, 5));
        graph.AddEdge("B", "D", random.Next(1, 5));
        graph.AddEdge("A", "C", random.Next(1, 5));
        graph.AddEdge("C", "D", random.Next(1, 5));

        if (random.NextDouble() > 0.5)
        {
            graph.AddEdge("B", "C", random.Next(1, 5));
        }

        return graph;
    }
}