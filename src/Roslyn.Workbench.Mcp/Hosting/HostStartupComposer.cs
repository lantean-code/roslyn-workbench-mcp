namespace Roslyn.Workbench.Mcp.Hosting;

internal static class HostStartupComposer
{
    public static HostStartupComposition Compose(string[] args)
    {
        var pathComparison = new WorkspacePathComparison();
        var configuration = StartupOptionsResolver.Resolve(args, pathComparison);
        var optionsValidator = new StartupOptionsValidator();
        optionsValidator.EnsureValid(configuration.Options);

        var codeActions = new CodeActionCatalogSnapshot
        {
            Tools = BundledCodeActionCatalog.Create(),
        };

        return new HostStartupComposition
        {
            Configuration = configuration,
            CodeActions = codeActions,
        };
    }
}
