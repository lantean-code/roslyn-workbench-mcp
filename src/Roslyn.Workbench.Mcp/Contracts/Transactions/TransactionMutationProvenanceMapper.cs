using System.Globalization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Maps retained transaction provenance to its agent-facing response shape.
/// </summary>
internal static class TransactionMutationProvenanceMapper
{
    /// <summary>
    /// Maps active transaction provenance and assigns response-local provider references.
    /// </summary>
    /// <param name="provenance">The retained transaction provenance.</param>
    /// <returns>The mapped provenance and optional provider dictionary.</returns>
    public static (IReadOnlyList<TransactionMutationProvenanceData> Provenance, IReadOnlyDictionary<string, MutationProviderIdentity>? Providers) Create(
        IReadOnlyList<TransactionMutationProvenance> provenance)
    {
        var providerIdsByIdentity = new Dictionary<MutationProviderIdentity, string>();
        var providersById = new Dictionary<string, MutationProviderIdentity>(StringComparer.Ordinal);
        var mappedProvenance = new List<TransactionMutationProvenanceData>(provenance.Count);

        foreach (var entry in provenance)
        {
            CodeActionMutationProvenanceData? codeAction = null;
            if (entry.CodeAction is { } retainedCodeAction)
            {
                var providerId = GetOrAddProvider(retainedCodeAction.Provider, providerIdsByIdentity, providersById);

                string? fixAllProviderId = null;
                if (retainedCodeAction.FixAllProvider is { } fixAllProvider)
                {
                    fixAllProviderId = GetOrAddProvider(fixAllProvider, providerIdsByIdentity, providersById);
                }

                codeAction = new CodeActionMutationProvenanceData
                {
                    Kind = retainedCodeAction.Kind,
                    ProviderId = providerId,
                    FixAllProviderId = fixAllProviderId,
                };
            }

            var mappedEntry = new TransactionMutationProvenanceData
            {
                Revision = entry.Revision,
                Operation = entry.Operation,
                Summary = entry.Summary,
                CodeAction = codeAction,
            };

            mappedProvenance.Add(mappedEntry);
        }

        return (mappedProvenance, providersById.Count == 0 ? null : providersById);
    }

    private static string GetOrAddProvider(
        MutationProviderIdentity provider,
        Dictionary<MutationProviderIdentity, string> providerIdsByIdentity,
        Dictionary<string, MutationProviderIdentity> providersById)
    {
        if (providerIdsByIdentity.TryGetValue(provider, out var providerId))
        {
            return providerId;
        }

        var providerNumber = providersById.Count + 1;
        providerId = $"p{providerNumber.ToString(CultureInfo.InvariantCulture)}";
        providerIdsByIdentity.Add(provider, providerId);
        providersById.Add(providerId, provider);
        return providerId;
    }
}
