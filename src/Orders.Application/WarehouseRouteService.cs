using BuildingBlocks;
using Orders.Domain;

namespace Orders.Application;

public sealed class WarehouseRouteService
{
    public Result<List<string>> GetRoute(WarehouseGraph graph, string startNode, string endNode)
    {
        if (graph is null) return Result<List<string>>.Failure("Graph is required.");
        return graph.FindShortestPath(startNode, endNode);
    }
}
