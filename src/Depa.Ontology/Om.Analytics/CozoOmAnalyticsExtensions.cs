using System.Text.Json;
using Depa.Cozo;
using Depa.Ontology;
using Depa.Ontology.Contracts.Models;

namespace Depa.Ontology.Analytics;

public static class CozoOmAnalyticsExtensions
{
    public static async Task<OmTemplateResult<ImpactAnalysisInput, ImpactAnalysisData, ImpactAnalysisStats>> ImpactAnalysisAsync(
        this CozoOm om,
        ImpactAnalysisInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(om);
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.RootId))
        {
            throw new ArgumentException("ImpactAnalysis requires input.RootId", nameof(input));
        }

        var normalized = input with
        {
            RelationNames = input.RelationNames?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray() ?? [],
            MaxDepth = Math.Max(0, input.MaxDepth),
        };
        var graph = await WalkImpactGraphAsync(om, normalized.RootId, normalized.RelationNames, normalized.MaxDepth, normalized.Direction, cancellationToken);
        var byType = CountByType(graph.Nodes);
        var visualGraph = BuildGraphVisual(graph.Nodes, graph.Edges, normalized.RootId);
        var data = new ImpactAnalysisData(
            graph.Nodes,
            graph.Edges,
            new ImpactAnalysisVisual("graph", visualGraph, new LegendVisual(byType)));
        var stats = new ImpactAnalysisStats(
            Math.Max(0, graph.Nodes.Count - 1),
            byType,
            graph.MaxDepthReached,
            graph.CycleDetected,
            graph.MaxDepthReached >= normalized.MaxDepth);

        return new OmTemplateResult<ImpactAnalysisInput, ImpactAnalysisData, ImpactAnalysisStats>(
            "impactAnalysis",
            "v1",
            normalized,
            data,
            stats,
            []);
    }

    public static async Task<OmTemplateResult<OwnershipTreeInput, OwnershipTreeData, OwnershipTreeStats>> OwnershipTreeAsync(
        this CozoOm om,
        OwnershipTreeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(om);
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.RootId))
        {
            throw new ArgumentException("OwnershipTree requires input.RootId", nameof(input));
        }

        var normalized = input with
        {
            OwnerRelationNames = input.OwnerRelationNames?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray() ?? ["owns", "contains"],
            MaxDepth = Math.Max(0, input.MaxDepth),
        };
        var graph = await WalkImpactGraphAsync(om, normalized.RootId, normalized.OwnerRelationNames, normalized.MaxDepth, OmAnalyticsDirection.Outgoing, cancellationToken);
        var visualGraph = BuildGraphVisual(graph.Nodes, graph.Edges, normalized.RootId);
        var visualTree = BuildTreeVisual(normalized.RootId, graph.Edges);
        var data = new OwnershipTreeData(
            normalized.RootId,
            graph.Nodes,
            graph.Edges,
            new OwnershipTreeVisual("tree", visualTree, visualGraph));
        var stats = new OwnershipTreeStats(
            graph.Nodes.Count,
            graph.Edges.Count,
            graph.MaxDepthReached,
            graph.CycleDetected,
            graph.MaxDepthReached >= normalized.MaxDepth);

        return new OmTemplateResult<OwnershipTreeInput, OwnershipTreeData, OwnershipTreeStats>(
            "ownershipTree",
            "v1",
            normalized,
            data,
            stats,
            []);
    }

    public static async Task<OmTemplateResult<RiskHotspotInput, RiskHotspotData, RiskHotspotStats>> RiskHotspotAsync(
        this CozoOm om,
        RiskHotspotInput? input = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(om);
        var normalized = input ?? new RiskHotspotInput();
        var topK = normalized.TopK > 0 ? normalized.TopK : 5;
        var entities = await om.FindByClassAsync(normalized.ClassName, cancellationToken: cancellationToken);
        var scored = new List<RiskHotspotEntry>();

        foreach (var entity in entities)
        {
            var value = await om.GetFieldValueAsync(entity.Id, normalized.RiskAttr, cancellationToken);
            if (!TryGetNumber(value, out var baseScore))
            {
                continue;
            }

            var neighbors = await om.GetNeighborsAsync(entity.Id, cancellationToken: cancellationToken);
            var degree = neighbors.Incoming.Count + neighbors.Outgoing.Count;
            var score = baseScore + normalized.DegreeWeight * degree;
            if (score < normalized.MinScore)
            {
                continue;
            }

            scored.Add(new RiskHotspotEntry(
                0,
                new RiskHotspotEntity(entity.Id, entity.Label, normalized.ClassName),
                score,
                new RiskHotspotFactors(baseScore, degree, normalized.DegreeWeight)));
        }

        var hotspots = scored
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Entity.Id, StringComparer.Ordinal)
            .Take(topK)
            .Select((entry, index) => entry with { Rank = index + 1 })
            .ToArray();
        var visual = BuildRankingVisual(hotspots);
        var data = new RiskHotspotData(hotspots, visual);
        var stats = new RiskHotspotStats(entities.Count, hotspots.Length);

        return new OmTemplateResult<RiskHotspotInput, RiskHotspotData, RiskHotspotStats>(
            "riskHotspot",
            "v1",
            normalized with { TopK = topK },
            data,
            stats,
            []);
    }

    private static async Task<WalkGraphResult> WalkImpactGraphAsync(
        CozoOm om,
        string rootId,
        IReadOnlyList<string>? relNames,
        int maxDepth,
        OmAnalyticsDirection direction,
        CancellationToken cancellationToken)
    {
        var rootView = await om.GetObjectViewAsync(rootId, cancellationToken) ??
            throw new CozoException($"Root entity '{rootId}' does not exist");
        var relationFilters = relNames ?? [];
        var queue = new Queue<(string Id, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { rootId };
        var nodes = new Dictionary<string, AnalyticsNode>(StringComparer.Ordinal)
        {
            [rootId] = new(rootId, rootView.ClassName, rootView.Label, 0)
        };
        var edges = new Dictionary<string, AnalyticsRelationLink>(StringComparer.Ordinal);
        var maxDepthReached = 0;
        var cycleDetected = false;
        queue.Enqueue((rootId, 0));

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            if (current.Depth >= maxDepth)
            {
                continue;
            }

            if (relationFilters.Count > 0)
            {
                foreach (var relationName in relationFilters)
                {
                    await AddNeighborsAsync(relationName);
                }
            }
            else
            {
                await AddNeighborsAsync(null);
            }

            async Task AddNeighborsAsync(string? relationName)
            {
                var neighbors = await om.GetNeighborsAsync(current.Id, relationName, ToOmDirection(direction), cancellationToken);
                if (direction is OmAnalyticsDirection.Outgoing or OmAnalyticsDirection.Both)
                {
                    foreach (var entry in neighbors.Outgoing)
                    {
                        AddEdgeAndMaybeNode(
                            current.Id,
                            entry.ObjectId,
                            entry.RelationName,
                            "outgoing",
                            entry,
                            current.Depth);
                    }
                }

                if (direction is OmAnalyticsDirection.Incoming or OmAnalyticsDirection.Both)
                {
                    foreach (var entry in neighbors.Incoming)
                    {
                        AddEdgeAndMaybeNode(
                            entry.ObjectId,
                            current.Id,
                            entry.RelationName,
                            "incoming",
                            entry,
                            current.Depth);
                    }
                }
            }

            void AddEdgeAndMaybeNode(string fromId, string toId, string relationName, string edgeDirection, NeighborEntry entry, int currentDepth)
            {
                var neighborId = entry.ObjectId;
                edges[$"{fromId}|{relationName}|{toId}"] = new AnalyticsRelationLink(fromId, toId, relationName, edgeDirection);
                if (!nodes.ContainsKey(neighborId))
                {
                    nodes[neighborId] = new AnalyticsNode(neighborId, entry.ClassName, entry.Label, currentDepth + 1);
                }

                if (visited.Contains(neighborId))
                {
                    cycleDetected = true;
                    return;
                }

                visited.Add(neighborId);
                queue.Enqueue((neighborId, currentDepth + 1));
                maxDepthReached = Math.Max(maxDepthReached, currentDepth + 1);
            }
        }

        return new WalkGraphResult(nodes.Values.ToArray(), edges.Values.ToArray(), cycleDetected, maxDepthReached);
    }

    private static OmDirection ToOmDirection(OmAnalyticsDirection direction) => direction switch
    {
        OmAnalyticsDirection.Outgoing => OmDirection.Outgoing,
        OmAnalyticsDirection.Incoming => OmDirection.Incoming,
        _ => OmDirection.Both,
    };

    private static IReadOnlyDictionary<string, int> CountByType(IReadOnlyList<AnalyticsNode> nodes)
    {
        return nodes
            .GroupBy(node => node.ClassName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static GraphVisual BuildGraphVisual(IReadOnlyList<AnalyticsNode> nodes, IReadOnlyList<AnalyticsRelationLink> edges, string rootId)
    {
        var nodeMap = new Dictionary<string, GraphVisualNode>(StringComparer.Ordinal);
        var adjacency = nodes.ToDictionary(
            node => node.Id,
            _ => (IReadOnlyList<GraphAdjacencyEntry>)[],
            StringComparer.Ordinal);
        var adjacencyMutable = nodes.ToDictionary(
            node => node.Id,
            _ => new List<GraphAdjacencyEntry>(),
            StringComparer.Ordinal);

        var visualNodes = nodes.Select(node =>
        {
            var visual = new GraphVisualNode(
                node.Id,
                node.Label,
                node.ClassName,
                node.ClassName,
                node.Depth,
                new Dictionary<string, double>(),
                new GraphNodeFlags(node.Id == rootId));
            nodeMap[node.Id] = visual;
            return visual;
        }).ToArray();

        var visualEdges = edges.Select((edge, index) =>
        {
            if (!adjacencyMutable.TryGetValue(edge.FromId, out var list))
            {
                list = [];
                adjacencyMutable[edge.FromId] = list;
            }

            list.Add(new GraphAdjacencyEntry(edge.ToId, edge.RelationName, edge.Direction));
            return new GraphVisualEdge(
                $"e:{index}:{edge.FromId}:{edge.RelationName}:{edge.ToId}",
                edge.FromId,
                edge.ToId,
                edge.RelationName,
                edge.RelationName,
                edge.Direction,
                1,
                new Dictionary<string, bool>());
        }).ToArray();

        foreach (var pair in adjacencyMutable)
        {
            adjacency[pair.Key] = pair.Value;
        }

        return new GraphVisual(visualNodes, visualEdges, nodeMap, adjacency);
    }

    private static TreeVisual BuildTreeVisual(string rootId, IReadOnlyList<AnalyticsRelationLink> edges)
    {
        var childrenById = new Dictionary<string, List<TreeChildEntry>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!childrenById.TryGetValue(edge.FromId, out var children))
            {
                children = [];
                childrenById[edge.FromId] = children;
            }

            children.Add(new TreeChildEntry(edge.ToId, edge.RelationName, edge.Direction));
        }

        return new TreeVisual(
            rootId,
            childrenById.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<TreeChildEntry>)pair.Value,
                StringComparer.Ordinal),
            []);
    }

    private static RiskHotspotVisual BuildRankingVisual(IReadOnlyList<RiskHotspotEntry> hotspots)
    {
        var ranking = hotspots
            .Select(hotspot => new RankingVisualEntry(hotspot.Rank, hotspot.Entity.Id, hotspot.Entity.Label, hotspot.Score, hotspot.Factors))
            .ToArray();
        return new RiskHotspotVisual(
            "ranking",
            ranking,
            new RankingSeries(ranking.Select(entry => entry.Label).ToArray(), ranking.Select(entry => entry.Score).ToArray()));
    }

    private static bool TryGetNumber(JsonElement? value, out double number)
    {
        number = 0;
        return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out number);
    }

    private sealed record WalkGraphResult(
        IReadOnlyList<AnalyticsNode> Nodes,
        IReadOnlyList<AnalyticsRelationLink> Edges,
        bool CycleDetected,
        int MaxDepthReached);
}
