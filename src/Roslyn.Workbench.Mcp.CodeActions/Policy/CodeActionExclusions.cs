using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

internal static class CodeActionExclusions
{
    internal const string _editorStateRequired = "EditorStateRequired";
    internal const string _externalIntelligenceRequired = "ExternalIntelligenceRequired";
    internal const string _optionsRequired = "OptionsRequired";
    internal const string _packageMutationRequired = "PackageMutationRequired";
    internal const string _projectMutationRequired = "ProjectMutationRequired";

    internal static FrozenDictionary<string, string> ProviderReasons { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider"] = _optionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider"] = _projectMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider"] = _packageMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider"] = _editorStateRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider"] = _optionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider"] = _optionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider"] = _externalIntelligenceRequired,
            ["Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider"] = _externalIntelligenceRequired,
            ["Microsoft.CodeAnalysis.CSharp.UpdateProjectToAllowUnsafe.CSharpUpdateProjectToAllowUnsafeCodeFixProvider"] = _projectMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.UpgradeProject.CSharpUpgradeProjectCodeFixProvider"] = _projectMutationRequired,
            ["Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider"] = _optionsRequired,
            ["Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider"] = _optionsRequired,
            ["Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider"] = _optionsRequired,
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
