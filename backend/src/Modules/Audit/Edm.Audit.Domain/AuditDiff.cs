using System.Text.Json.Nodes;

namespace Edm.Audit.Domain;

/// <summary>One changed field. Nested fields use dotted paths, for example "address.city".</summary>
public sealed record FieldChange(string Field, JsonNode? Before, JsonNode? After);

/// <summary>
/// Computes the field-level difference between a before and an after snapshot (Part 10.1).
/// Objects are compared key by key, recursively. A missing side counts as an empty object, so a create
/// lists every field as a change from nothing. Other values are compared as whole JSON values.
/// </summary>
public static class AuditDiff
{
    public static IReadOnlyList<FieldChange> Compute(JsonNode? before, JsonNode? after)
    {
        var changes = new List<FieldChange>();
        Walk(string.Empty, before, after, changes);
        return changes;
    }

    private static void Walk(string path, JsonNode? before, JsonNode? after, List<FieldChange> changes)
    {
        var bothObjectsOrMissing = (before is null or JsonObject) && (after is null or JsonObject);
        if (bothObjectsOrMissing && (before is JsonObject || after is JsonObject))
        {
            var beforeObject = before as JsonObject;
            var afterObject = after as JsonObject;
            var keys = KeysOf(beforeObject)
                .Union(KeysOf(afterObject), StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal);

            foreach (var key in keys)
            {
                var beforeValue = beforeObject is not null && beforeObject.TryGetPropertyValue(key, out var b) ? b : null;
                var afterValue = afterObject is not null && afterObject.TryGetPropertyValue(key, out var a) ? a : null;
                Walk(path.Length == 0 ? key : $"{path}.{key}", beforeValue, afterValue, changes);
            }

            return;
        }

        if (before is null && after is null)
        {
            return;
        }

        if (before is not null && after is not null && JsonNode.DeepEquals(before, after))
        {
            return;
        }

        changes.Add(new FieldChange(
            path.Length == 0 ? "(root)" : path,
            before?.DeepClone(),
            after?.DeepClone()));
    }

    private static IEnumerable<string> KeysOf(JsonObject? obj)
        => obj is null ? Enumerable.Empty<string>() : obj.Select(pair => pair.Key);
}
