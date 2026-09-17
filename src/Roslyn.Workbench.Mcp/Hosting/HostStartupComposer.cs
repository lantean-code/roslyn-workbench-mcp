namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Resolves all pre-container startup state required to construct the MCP host.
/// </summary>
internal static class HostStartupComposer
{
    /// <summary>
    /// Resolves startup configuration and creates the host's bundled Code Action catalogue.
    /// </summary>
    /// <param name="args">The command-line arguments used to resolve startup options.</param>
    /// <returns>The validated startup configuration and bundled Code Action catalogue.</returns>
    public static HostStartupComposition Compose(string[] args)
    {
        var pathComparison = new WorkspacePathComparison();
        var configuration = StartupOptionsResolver.Resolve(args, pathComparison);
        var optionsValidator = new StartupOptionsValidator();
        optionsValidator.EnsureValid(configuration.Options);
        var policy = OperationalPolicyResolver.Resolve(
            configuration.Options.OperationalMode,
            configuration.Options.CommitValidation);

        var codeActions = new CodeActionCatalogSnapshot
        {
            ReservedToolNames = BundledCodeActionCatalog.ToolNames,
            Tools = BundledCodeActionCatalog.Create(policy.SourceMutationEnabled),
        };

        return new HostStartupComposition
        {
            Configuration = configuration,
            CodeActions = codeActions,
            Policy = policy,
        };
    }
}
