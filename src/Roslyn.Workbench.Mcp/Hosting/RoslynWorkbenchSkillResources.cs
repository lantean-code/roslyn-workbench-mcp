namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Publishes the version-matched, embedded agent skill as fixed offline MCP resources cached for the process lifetime.
/// </summary>
[McpServerResourceType]
internal static class RoslynWorkbenchSkillResources
{
    private static readonly Lazy<string> _skill = new(static () => ReadEmbeddedFile("SKILL.md"));
    private static readonly Lazy<string> _workspaces = new(static () => ReadEmbeddedFile("references/workspaces.md"));
    private static readonly Lazy<string> _selectors = new(static () => ReadEmbeddedFile("references/selectors.md"));
    private static readonly Lazy<string> _navigation = new(static () => ReadEmbeddedFile("references/navigation.md"));
    private static readonly Lazy<string> _structure = new(static () => ReadEmbeddedFile("references/structure.md"));
    private static readonly Lazy<string> _diagnostics = new(static () => ReadEmbeddedFile("references/diagnostics.md"));
    private static readonly Lazy<string> _flowAnalysis = new(static () => ReadEmbeddedFile("references/flow-analysis.md"));
    private static readonly Lazy<string> _impactAnalysis = new(static () => ReadEmbeddedFile("references/impact-analysis.md"));
    private static readonly Lazy<string> _transactions = new(static () => ReadEmbeddedFile("references/transactions.md"));
    private static readonly Lazy<string> _codeActions = new(static () => ReadEmbeddedFile("references/code-actions.md"));
    private static readonly Lazy<string> _renameFormat = new(static () => ReadEmbeddedFile("references/rename-format.md"));
    private static readonly Lazy<string> _recovery = new(static () => ReadEmbeddedFile("references/recovery.md"));

    /// <summary>
    /// Reads the bundled skill entry point and task-specific workflow links.
    /// </summary>
    /// <returns>The bundled skill entry point and task-specific workflow links.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/SKILL.md", Name = "roslyn-workbench", MimeType = "text/markdown")]
    [Description("Roslyn Workbench skill entry point and task-specific workflow links.")]
    public static string ReadSkill()
    {
        return _skill.Value;
    }

    /// <summary>
    /// Reads the bundled workspace selection, loading, freshness and lifecycle guidance.
    /// </summary>
    /// <returns>The bundled workspace selection, loading, freshness and lifecycle guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/workspaces.md", Name = "roslyn-workbench-workspaces", MimeType = "text/markdown")]
    [Description("Roslyn Workbench workspace selection, loading, freshness and lifecycle guidance.")]
    public static string ReadWorkspaces()
    {
        return _workspaces.Value;
    }

    /// <summary>
    /// Reads the bundled selector reuse, snapshot identity and bounded-result guidance.
    /// </summary>
    /// <returns>The bundled selector reuse, snapshot identity and bounded-result guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/selectors.md", Name = "roslyn-workbench-selectors", MimeType = "text/markdown")]
    [Description("Roslyn Workbench selector reuse, snapshot identity and bounded-result guidance.")]
    public static string ReadSelectors()
    {
        return _selectors.Value;
    }

    /// <summary>
    /// Reads the bundled symbol navigation, references and call analysis guidance.
    /// </summary>
    /// <returns>The bundled symbol navigation, references and call analysis guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/navigation.md", Name = "roslyn-workbench-navigation", MimeType = "text/markdown")]
    [Description("Roslyn Workbench symbol navigation, references and call analysis guidance.")]
    public static string ReadNavigation()
    {
        return _navigation.Value;
    }

    /// <summary>
    /// Reads the bundled project structure, configuration, symbol relationships and API guidance.
    /// </summary>
    /// <returns>The bundled project structure, configuration, symbol relationships and API guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/structure.md", Name = "roslyn-workbench-structure", MimeType = "text/markdown")]
    [Description("Roslyn Workbench project structure, configuration, symbol relationships and API guidance.")]
    public static string ReadStructure()
    {
        return _structure.Value;
    }

    /// <summary>
    /// Reads the bundled compiler, analyser and focused diagnostic guidance.
    /// </summary>
    /// <returns>The bundled compiler, analyser and focused diagnostic guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/diagnostics.md", Name = "roslyn-workbench-diagnostics", MimeType = "text/markdown")]
    [Description("Roslyn Workbench compiler, analyser and focused diagnostic guidance.")]
    public static string ReadDiagnostics()
    {
        return _diagnostics.Value;
    }

    /// <summary>
    /// Reads the bundled data flow, control flow, operation tree and graph guidance.
    /// </summary>
    /// <returns>The bundled data flow, control flow, operation tree and graph guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/flow-analysis.md", Name = "roslyn-workbench-flow-analysis", MimeType = "text/markdown")]
    [Description("Roslyn Workbench data flow, control flow, operation tree and graph guidance.")]
    public static string ReadFlowAnalysis()
    {
        return _flowAnalysis.Value;
    }

    /// <summary>
    /// Reads the bundled dependency, change impact, test impact and cleanup guidance.
    /// </summary>
    /// <returns>The bundled dependency, change impact, test impact and cleanup guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/impact-analysis.md", Name = "roslyn-workbench-impact-analysis", MimeType = "text/markdown")]
    [Description("Roslyn Workbench dependency, change impact, test impact and cleanup guidance.")]
    public static string ReadImpactAnalysis()
    {
        return _impactAnalysis.Value;
    }

    /// <summary>
    /// Reads the bundled transaction staging, review, validation and persistence guidance.
    /// </summary>
    /// <returns>The bundled transaction staging, review, validation and persistence guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/transactions.md", Name = "roslyn-workbench-transactions", MimeType = "text/markdown")]
    [Description("Roslyn Workbench transaction staging, review, validation and persistence guidance.")]
    public static string ReadTransactions()
    {
        return _transactions.Value;
    }

    /// <summary>
    /// Reads the bundled Code Action discovery, staging and Fix All guidance.
    /// </summary>
    /// <returns>The bundled Code Action discovery, staging and Fix All guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/code-actions.md", Name = "roslyn-workbench-code-actions", MimeType = "text/markdown")]
    [Description("Roslyn Workbench Code Action discovery, staging and Fix All guidance.")]
    public static string ReadCodeActions()
    {
        return _codeActions.Value;
    }

    /// <summary>
    /// Reads the bundled symbol renaming and source formatting guidance.
    /// </summary>
    /// <returns>The bundled symbol renaming and source formatting guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/rename-format.md", Name = "roslyn-workbench-rename-format", MimeType = "text/markdown")]
    [Description("Roslyn Workbench symbol renaming and source formatting guidance.")]
    public static string ReadRenameFormat()
    {
        return _renameFormat.Value;
    }

    /// <summary>
    /// Reads the bundled failed operations, durable recovery and error-reporting guidance.
    /// </summary>
    /// <returns>The bundled failed operations, durable recovery and error-reporting guidance.</returns>
    [McpServerResource(UriTemplate = "skill://roslyn-workbench/references/recovery.md", Name = "roslyn-workbench-recovery", MimeType = "text/markdown")]
    [Description("Roslyn Workbench failed operations, durable recovery and error-reporting guidance.")]
    public static string ReadRecovery()
    {
        return _recovery.Value;
    }

    /// <summary>
    /// Reads a packaged skill file by its assembly resource name suffix.
    /// </summary>
    /// <param name="relativePath">The canonical skill-relative path.</param>
    /// <returns>The exact UTF-8 file content, preserving line endings.</returns>
    public static string ReadEmbeddedFile(string relativePath)
    {
        var assembly = typeof(RoslynWorkbenchSkillResources).Assembly;
        using var stream = assembly.GetManifestResourceStream($"RoslynWorkbenchSkill/{relativePath}")
            ?? throw new InvalidOperationException($"The bundled Roslyn Workbench skill file '{relativePath}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
