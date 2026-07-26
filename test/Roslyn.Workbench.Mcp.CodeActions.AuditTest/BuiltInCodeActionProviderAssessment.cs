namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal static class BuiltInCodeActionProviderAssessment
{
    internal static IReadOnlyList<BuiltInCodeActionProviderAssessmentEntry> Entries { get; } =
    [
        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider",
            BuiltInCodeActionAuditStatus.CoveredByDedicatedTool),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.GenerateEqualsAndGetHashCodeFromMembers.GenerateEqualsAndGetHashCodeFromMembersCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresActionLevelClassification),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.PreferFrameworkType.PreferFrameworkTypeCodeFixProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.GenerateConstructors.CSharpGenerateConstructorsCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresActionLevelClassification),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PullMemberUp.CSharpPullMemberUpCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateType.GenerateTypeCodeFixProvider",
            BuiltInCodeActionAuditStatus.RequiresActionLevelClassification),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.EmbeddedLanguages.CSharpJsonDetectionCodeFixProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.SimplifyThisOrMe.CSharpSimplifyThisOrMeCodeFixProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.SimplifyTypeNames.SimplifyTypeNamesCodeFixProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpIsAndCastCheckWithoutNameCodeFixProvider",
            BuiltInCodeActionAuditStatus.RequiresDedicatedImplementation),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),
    ];

    private static BuiltInCodeActionProviderAssessmentEntry CreateRefactoring(
        string providerId,
        BuiltInCodeActionAuditStatus status)
    {
        return new BuiltInCodeActionProviderAssessmentEntry
        {
            ProviderId = providerId,
            Kind = BuiltInCodeActionFamilyKind.Refactoring,
            Status = status,
        };
    }

    private static BuiltInCodeActionProviderAssessmentEntry CreateCodeFix(
        string providerId,
        BuiltInCodeActionAuditStatus status)
    {
        return new BuiltInCodeActionProviderAssessmentEntry
        {
            ProviderId = providerId,
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            Status = status,
        };
    }
}
