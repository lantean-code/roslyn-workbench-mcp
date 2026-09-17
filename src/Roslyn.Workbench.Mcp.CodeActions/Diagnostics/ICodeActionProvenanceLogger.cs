namespace Roslyn.Workbench.Mcp.CodeActions.Diagnostics;

/// <summary>
/// Records structured attribution for Code Actions that progress beyond discovery.
/// </summary>
internal interface ICodeActionProvenanceLogger
{
    /// <summary>
    /// Records a Fix All operation after its immutable prepared reference has been created.
    /// </summary>
    /// <param name="provenance">The prepared Fix All attribution and audit metadata.</param>
    void LogPreparedFixAll(CodeActionMutationProvenance provenance);

    /// <summary>
    /// Records a Code Action after its candidate has been staged successfully.
    /// </summary>
    /// <param name="provenance">The staged Code Action attribution and audit metadata.</param>
    void LogStaged(CodeActionMutationProvenance provenance);
}
