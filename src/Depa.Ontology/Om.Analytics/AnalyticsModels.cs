namespace Depa.Ontology.Analytics;

public enum OmAnalyticsDirection
{
    Outgoing,
    Incoming,
    Both,
}

public sealed record ImpactAnalysisInput(
    string RootId,
    IReadOnlyList<string>? RelationNames = null,
    int MaxDepth = 2,
    OmAnalyticsDirection Direction = OmAnalyticsDirection.Outgoing);

public sealed record OwnershipTreeInput(
    string RootId,
    IReadOnlyList<string>? OwnerRelationNames = null,
    int MaxDepth = 3);

public sealed record RiskHotspotInput(
    string ClassName = "Task",
    string RiskAttr = "estimate_hours",
    int TopK = 5,
    double MinScore = 0,
    double DegreeWeight = 1);

public sealed record OmTemplateResult<TInput, TData, TStats>(
    string Template,
    string Version,
    TInput Input,
    TData Data,
    TStats Stats,
    IReadOnlyList<string> Warnings);

public sealed record ImpactAnalysisData(
    IReadOnlyList<AnalyticsNode> Nodes,
    IReadOnlyList<AnalyticsRelationLink> Edges,
    ImpactAnalysisVisual Visual);

public sealed record ImpactAnalysisVisual(string Primary, GraphVisual Graph, LegendVisual Legend);

public sealed record ImpactAnalysisStats(
    int ImpactedCount,
    IReadOnlyDictionary<string, int> ByType,
    int MaxDepthReached,
    bool CycleDetected,
    bool Truncated);

public sealed record OwnershipTreeData(
    string RootId,
    IReadOnlyList<AnalyticsNode> Nodes,
    IReadOnlyList<AnalyticsRelationLink> Edges,
    OwnershipTreeVisual Visual);

public sealed record OwnershipTreeVisual(string Primary, TreeVisual Tree, GraphVisual Graph);

public sealed record OwnershipTreeStats(
    int NodeCount,
    int EdgeCount,
    int MaxDepthReached,
    bool CycleDetected,
    bool Truncated);

public sealed record RiskHotspotData(IReadOnlyList<RiskHotspotEntry> Hotspots, RiskHotspotVisual Visual);

public sealed record RiskHotspotVisual(string Primary, IReadOnlyList<RankingVisualEntry> Ranking, RankingSeries Series);

public sealed record RiskHotspotStats(int EvaluatedCount, int ReturnedCount);

public sealed record AnalyticsNode(string Id, string ClassName, string Label, int Depth);

public sealed record AnalyticsRelationLink(string FromId, string ToId, string RelationName, string Direction);

public sealed record GraphVisual(
    IReadOnlyList<GraphVisualNode> Nodes,
    IReadOnlyList<GraphVisualEdge> Edges,
    IReadOnlyDictionary<string, GraphVisualNode> NodeMap,
    IReadOnlyDictionary<string, IReadOnlyList<GraphAdjacencyEntry>> Adjacency);

public sealed record GraphVisualNode(
    string Id,
    string Label,
    string Kind,
    string Group,
    int Depth,
    IReadOnlyDictionary<string, double> Metrics,
    GraphNodeFlags Flags);

public sealed record GraphNodeFlags(bool IsRoot);

public sealed record GraphVisualEdge(
    string Id,
    string Source,
    string Target,
    string Kind,
    string Label,
    string Direction,
    double Weight,
    IReadOnlyDictionary<string, bool> Flags);

public sealed record GraphAdjacencyEntry(string ToId, string RelationName, string Direction);

public sealed record LegendVisual(IReadOnlyDictionary<string, int> ByType);

public sealed record TreeVisual(
    string RootId,
    IReadOnlyDictionary<string, IReadOnlyList<TreeChildEntry>> ChildrenById,
    IReadOnlyList<AnalyticsRelationLink> CrossEdges);

public sealed record TreeChildEntry(string ToId, string RelationName, string Direction);

public sealed record RiskHotspotEntry(int Rank, RiskHotspotEntity Entity, double Score, RiskHotspotFactors Factors);

public sealed record RiskHotspotEntity(string Id, string Label, string ClassName);

public sealed record RiskHotspotFactors(double BaseScore, int Degree, double DegreeWeight);

public sealed record RankingVisualEntry(int Rank, string Id, string Label, double Score, RiskHotspotFactors Factors);

public sealed record RankingSeries(IReadOnlyList<string> Labels, IReadOnlyList<double> Values);
