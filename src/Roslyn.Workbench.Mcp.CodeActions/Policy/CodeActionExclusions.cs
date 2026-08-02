using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

internal static class CodeActionExclusions
{
    private const string _editorStateRequired = "EditorStateRequired";
    private const string _externalIntelligenceRequired = "ExternalIntelligenceRequired";
    public const string OptionsRequired = "OptionsRequired";
    private const string _packageMutationRequired = "PackageMutationRequired";
    private const string _projectMutationRequired = "ProjectMutationRequired";

    public static FrozenDictionary<string, string> ProviderReasons { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider"] = _projectMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider"] = _packageMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider"] = _editorStateRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider"] = _externalIntelligenceRequired,
            ["Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider"] = _externalIntelligenceRequired,
            ["Microsoft.CodeAnalysis.CSharp.UpdateProjectToAllowUnsafe.CSharpUpdateProjectToAllowUnsafeCodeFixProvider"] = _projectMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.UpgradeProject.CSharpUpgradeProjectCodeFixProvider"] = _projectMutationRequired,
            ["Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider"] = OptionsRequired,
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
