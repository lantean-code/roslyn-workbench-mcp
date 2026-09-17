namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Creates immutable mutation provenance from resolved Code Actions and provider identities.
/// </summary>
internal static class CodeActionMutationProvenanceFactory
{
    /// <summary>
    /// Creates provenance for an ordinary Code Fix or refactoring.
    /// </summary>
    /// <param name="action">The resolved action whose audit fields are retained.</param>
    /// <param name="provider">The originating provider identity captured by the catalogue.</param>
    /// <returns>The mutation provenance for staging and local logging.</returns>
    public static CodeActionMutationProvenance Create(
        DiscoveredCodeAction action,
        MutationProviderIdentity provider)
    {
        var kind = action.Kind == DiscoveredActionKind.CodeFix
            ? CodeActionMutationKind.CodeFix
            : CodeActionMutationKind.Refactoring;

        return CreateCore(action, provider, kind, fixAllProvider: null, fixAllScope: null);
    }

    /// <summary>
    /// Creates provenance for a prepared Fix All operation.
    /// </summary>
    /// <param name="action">The originating resolved Code Fix.</param>
    /// <param name="provider">The originating Code Fix provider identity.</param>
    /// <param name="fixAllProvider">The provider that assembled the Fix All action.</param>
    /// <param name="scope">The requested Fix All scope.</param>
    /// <returns>The mutation provenance for staging and local logging.</returns>
    public static CodeActionMutationProvenance CreateFixAll(
        DiscoveredCodeAction action,
        MutationProviderIdentity provider,
        MutationProviderIdentity fixAllProvider,
        CodeActionFixAllScope scope)
    {
        return CreateCore(
            action,
            provider,
            CodeActionMutationKind.FixAll,
            fixAllProvider,
            scope.ToString());
    }

    private static CodeActionMutationProvenance CreateCore(
        DiscoveredCodeAction action,
        MutationProviderIdentity provider,
        CodeActionMutationKind kind,
        MutationProviderIdentity? fixAllProvider,
        string? fixAllScope)
    {
        var diagnosticIds = action.DiagnosticIds
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new CodeActionMutationProvenance
        {
            Kind = kind,
            Provider = provider,
            FixAllProvider = fixAllProvider,
            DiagnosticIds = diagnosticIds,
            EquivalenceKey = action.EquivalenceKey,
            FixAllScope = fixAllScope,
        };
    }
}
