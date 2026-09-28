namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Validates authored invariant content, exact surface mappings and executable evidence references.
/// </summary>
internal static class SecurityManifestValidator
{
    private static readonly HashSet<string> _classifications = new(StringComparer.Ordinal)
    {
        "enforced",
        "delegated",
        "limitation",
    };

    /// <summary>
    /// Validates an authored manifest against the current deterministic surface.
    /// </summary>
    /// <param name="manifest">The authored manifest.</param>
    /// <param name="surface">The current generated surface.</param>
    public static void Validate(
        SecurityInvariantManifest manifest,
        IReadOnlyList<SecuritySurfaceItem> surface)
    {
        if (manifest.Format != "roslyn-workbench-security-invariants/v1")
        {
            throw new InvalidOperationException($"Unsupported security invariant manifest format '{manifest.Format}'.");
        }

        if (manifest.Entries.Count == 0)
        {
            throw new InvalidOperationException("The security invariant manifest must contain at least one entry.");
        }

        var duplicateId = manifest.Entries.GroupBy(static entry => entry.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Security invariant id '{duplicateId.Key}' is duplicated.");
        }

        var knownSurface = surface.Select(static item => item.Key).ToHashSet(StringComparer.Ordinal);
        var mappedSurface = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Entries)
        {
            ValidateEntry(entry);
            foreach (var key in entry.SurfaceKeys)
            {
                if (!knownSurface.Contains(key))
                {
                    throw new InvalidOperationException($"Security invariant '{entry.Id}' refers to unknown surface key '{key}'.");
                }

                mappedSurface.Add(key);
            }
        }

        var unmapped = knownSurface.Except(mappedSurface, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unmapped.Length > 0)
        {
            throw new InvalidOperationException($"Security surface keys are not mapped: {string.Join(", ", unmapped)}.");
        }
    }

    private static void ValidateEntry(SecurityInvariantEntry entry)
    {
        RequireContent(entry.Id, nameof(entry.Id), entry.Id);
        RequireContent(entry.Id, nameof(entry.Title), entry.Title);
        RequireContent(entry.Id, nameof(entry.Actor), entry.Actor);
        RequireContent(entry.Id, nameof(entry.Outcome), entry.Outcome);
        RequireContent(entry.Id, nameof(entry.Boundary), entry.Boundary);
        RequireContent(entry.Id, nameof(entry.ResidualRisk), entry.ResidualRisk);

        if (!_classifications.Contains(entry.Classification))
        {
            throw new InvalidOperationException($"Security invariant '{entry.Id}' uses unsupported classification '{entry.Classification}'.");
        }

        var duplicateSurface = entry.SurfaceKeys.GroupBy(static key => key, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateSurface is not null)
        {
            throw new InvalidOperationException($"Security invariant '{entry.Id}' maps surface key '{duplicateSurface.Key}' more than once.");
        }

        if (entry.Classification == "enforced" && entry.Evidence.Count == 0)
        {
            throw new InvalidOperationException($"Enforced security invariant '{entry.Id}' must identify executable evidence.");
        }

        if (entry.Classification == "enforced" && entry.SurfaceKeys.Count == 0)
        {
            throw new InvalidOperationException($"Enforced security invariant '{entry.Id}' must map at least one code-derived surface key.");
        }

        var duplicateEvidence = entry.Evidence
            .GroupBy(static evidence => (evidence.Project, evidence.Test))
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateEvidence is not null)
        {
            throw new InvalidOperationException($"Security invariant '{entry.Id}' identifies evidence test '{duplicateEvidence.Key.Test}' more than once for project '{duplicateEvidence.Key.Project}'.");
        }
    }

    private static void RequireContent(string id, string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Security invariant '{id}' must provide non-empty {field} content.");
        }
    }
}
