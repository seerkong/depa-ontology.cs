using Depa.Cozo;
using Depa.Ontology;
using Depa.Ontology.Inputs;
using Depa.Ontology.Logic;
using Depa.Ontology.Runtime;

namespace Depa.Ontology.Batch;

public static class CozoOmBatchExtensions
{
    public static async Task<OmBatchResult> IngestBatchAsync(
        this CozoOm om,
        OmBatchInput batch,
        OmBatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(om);
        ArgumentNullException.ThrowIfNull(batch);

        var objects = batch.Objects ?? [];
        var fieldValues = batch.FieldValues ?? [];
        var relationLinks = batch.RelationLinks ?? [];
        var touchedObjectIds = new HashSet<string>(StringComparer.Ordinal);
        var writeOptions = new WriteOptions(SkipConstraints: true);

        await using var tx = await om.Runtime.Store.BeginTransactionAsync(write: true, cancellationToken);
        var txRuntime = om.Runtime with { Store = tx };

        foreach (var omObject in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ObjectLogic.CreateObjectAsync(txRuntime, new ObjectInput(omObject.Id, omObject.ClassName, omObject.Label), cancellationToken);
            touchedObjectIds.Add(omObject.Id);
        }

        foreach (var fieldValue in fieldValues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ObjectLogic.SetFieldValueAsync(txRuntime, new SetFieldValueInput(fieldValue.ObjectId, fieldValue.FieldName, fieldValue.Value, writeOptions), cancellationToken);
            touchedObjectIds.Add(fieldValue.ObjectId);
        }

        foreach (var relationLink in relationLinks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RelationLogic.CreateRelationLinkAsync(
                txRuntime,
                new CreateRelationLinkInput(
                    relationLink.FromObjectId,
                    relationLink.RelationName,
                    relationLink.ToObjectId,
                    relationLink.Payload ?? new Dictionary<string, object?>(),
                    writeOptions),
                cancellationToken);
        }

        if (options?.ValidateRequired != false)
        {
            foreach (var objectId in touchedObjectIds)
            {
                var validation = await ConstraintLogic.ValidateObjectAsync(txRuntime, objectId, cancellationToken);
                if (!validation.Valid)
                {
                    throw new CozoException(string.Join("; ", validation.Errors));
                }
            }
        }

        await tx.CommitAsync(cancellationToken);
        return new OmBatchResult(objects.Count, fieldValues.Count, relationLinks.Count, touchedObjectIds.Count);
    }
}
