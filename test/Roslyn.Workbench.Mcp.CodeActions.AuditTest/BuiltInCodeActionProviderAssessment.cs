namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal static class BuiltInCodeActionProviderAssessment
{
    public static IReadOnlyList<BuiltInCodeActionProviderAssessmentEntry> Entries { get; } =
    [
        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider",
            BuiltInCodeActionAuditStatus.ValidatedSupported),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.GenerateEqualsAndGetHashCodeFromMembers.GenerateEqualsAndGetHashCodeFromMembersCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresActionLevelClassification),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.PreferFrameworkType.PreferFrameworkTypeCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.GenerateConstructors.CSharpGenerateConstructorsCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.RequiresActionLevelClassification),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateRefactoring(
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PullMemberUp.CSharpPullMemberUpCodeRefactoringProvider",
            BuiltInCodeActionAuditStatus.Excluded),

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
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.SimplifyThisOrMe.CSharpSimplifyThisOrMeCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.SimplifyTypeNames.SimplifyTypeNamesCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

        CreateCodeFix(
            "Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpIsAndCastCheckWithoutNameCodeFixProvider",
            BuiltInCodeActionAuditStatus.Excluded),

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
            Kind = DiscoveredActionKind.Refactoring,
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
            Kind = DiscoveredActionKind.CodeFix,
            Status = status,
        };
    }
}
