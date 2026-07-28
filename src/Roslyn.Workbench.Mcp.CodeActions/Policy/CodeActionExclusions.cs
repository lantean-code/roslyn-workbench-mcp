using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

internal static class CodeActionExclusions
{
    internal const string EditorStateRequired = "EditorStateRequired";
    internal const string ExternalIntelligenceRequired = "ExternalIntelligenceRequired";
    internal const string OptionsRequired = "OptionsRequired";
    internal const string PackageMutationRequired = "PackageMutationRequired";
    internal const string ProjectMutationRequired = "ProjectMutationRequired";

    internal static FrozenDictionary<string, string> ProviderReasons { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider"] = ProjectMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider"] = PackageMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider"] = EditorStateRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider"] = ExternalIntelligenceRequired,
            ["Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider"] = ExternalIntelligenceRequired,
            ["Microsoft.CodeAnalysis.CSharp.UpdateProjectToAllowUnsafe.CSharpUpdateProjectToAllowUnsafeCodeFixProvider"] = ProjectMutationRequired,
            ["Microsoft.CodeAnalysis.CSharp.UpgradeProject.CSharpUpgradeProjectCodeFixProvider"] = ProjectMutationRequired,
            ["Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider"] = OptionsRequired,
            ["Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider"] = OptionsRequired,
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
