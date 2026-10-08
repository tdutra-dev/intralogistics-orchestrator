using BuildingBlocks;

namespace Orders.Domain;

public sealed record WarehouseNode(string Id, string Kind);
public sealed record WarehouseEdge(string From, string To, decimal TravelTimeMinutes, bool Blocked = false);

public sealed class WarehouseGraph
{
    private readonly Dictionary<string, WarehouseNode> _nodes = new();
    private readonly Dictionary<string, List<WarehouseEdge>> _edges = new();

    public void AddNode(string id, string kind) => _nodes[id] = new WarehouseNode(id, kind);

    public void AddEdge(string from, string to, decimal travelTimeMinutes)
    {
        if (!_edges.ContainsKey(from)) _edges[from] = new List<WarehouseEdge>();
        _edges[from].Add(new WarehouseEdge(from, to, travelTimeMinutes));
    }

    public void BlockNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("Node identifier is required.", nameof(nodeId));
        foreach (var kvp in _edges)
        {
            foreach (var edge in kvp.Value.Where(e => e.From == nodeId || e.To == nodeId))
            {
                _edges[kvp.Key] = _edges[kvp.Key].Where(e => e != edge).ToList();
            }
        }
    }

    public Result<List<string>> FindShortestPath(string start, string end)
    {
        if (!_nodes.ContainsKey(start) || !_nodes.ContainsKey(end))
        {
            return Result<List<string>>.Failure("Start or end node does not exist.");
        }

        var distances = _nodes.Keys.ToDictionary(key => key, _ => decimal.MaxValue);
        var previous = new Dictionary<string, string?>();
        var queue = new SortedSet<(decimal Distance, string Node)>();

        distances[start] = 0m;
        queue.Add((0m, start));

        while (queue.Count > 0)
        {
            var current = queue.First();
            queue.Remove(current);

            if (current.Node == end)
            {
                break;
            }

            foreach (var edge in _edges.TryGetValue(current.Node, out var value) ? value : Enumerable.Empty<WarehouseEdge>())
            {
                if (edge.Blocked) continue;

                var newDistance = distances[current.Node] + edge.TravelTimeMinutes;
                if (newDistance < distances[edge.To])
                {
                    distances[edge.To] = newDistance;
                    previous[edge.To] = current.Node;
                    queue.Add((newDistance, edge.To));
                }
            }
        }

        if (distances[end] == decimal.MaxValue)
        {
            return Result<List<string>>.Failure("No route available.");
        }

        var path = new List<string> { end };
        var currentNode = end;
        while (previous.ContainsKey(currentNode))
        {
            currentNode = previous[currentNode]!;
            path.Add(currentNode);
        }

        path.Reverse();
        return Result<List<string>>.Success(path);
    }
}
