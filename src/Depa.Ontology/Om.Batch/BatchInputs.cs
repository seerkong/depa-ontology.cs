namespace Depa.Ontology.Batch;

public sealed record OmBatchInput(
    IReadOnlyList<OmBatchObject>? Objects = null,
    IReadOnlyList<OmBatchFieldValue>? FieldValues = null,
    IReadOnlyList<OmBatchRelationLink>? RelationLinks = null);

public sealed record OmBatchObject(string Id, string ClassName, string Label = "");

public sealed record OmBatchFieldValue(string ObjectId, string FieldName, object? Value);

public sealed record OmBatchRelationLink(string FromObjectId, string RelationName, string ToObjectId, object? Payload = null);

public sealed record OmBatchOptions(bool ValidateRequired = true);

public sealed record OmBatchResult(int Objects, int FieldValues, int RelationLinks, int ValidatedObjects);
