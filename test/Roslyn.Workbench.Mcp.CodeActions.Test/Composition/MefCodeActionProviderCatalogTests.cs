using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class MefCodeActionProviderCatalogTests
{
    [Fact]
    public void GIVEN_NoProviderAssembliesAreConfigured_WHEN_ConstructingCatalogue_THEN_ShouldPublishUnavailableComposition()
    {
        var options = Options.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
        });

        var exportProvider = new Mock<IMefHostExportProviderCompatibilityAdapter>();

        var target = new MefCodeActionProviderCatalog(options, exportProvider.Object);

        target.Status.IsAvailable.Should().BeFalse();
        target.Status.Version.Should().BeNull();
        target.Status.Message.Should().Be("No code-action provider assemblies were configured.");
        target.WorkspaceHostServices.Should().BeNull();
        target.RefactoringProviders.Should().BeEmpty();
        target.CodeFixProviders.Should().BeEmpty();
        exportProvider.Verify(item => item.ReadExports<CodeRefactoringProvider>(It.IsAny<MefHostServices>()), Times.Never);
        exportProvider.Verify(item => item.ReadExports<CodeFixProvider>(It.IsAny<MefHostServices>()), Times.Never);
    }

    [Fact]
    public void GIVEN_SuccessfulExportReadResult_WHEN_ReadingState_THEN_ShouldExposeExportsWithoutError()
    {
        IReadOnlyList<string> exports = ["Export"];

        var result = MefHostExportReadResult.Success(exports);

        result.IsSuccessful.Should().BeTrue();
        result.Exports.Should().BeSameAs(exports);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_FailedExportReadResult_WHEN_ReadingState_THEN_ShouldExposeErrorWithoutExports()
    {
        var result = MefHostExportReadResult.Failure<string>("Error");

        result.IsSuccessful.Should().BeFalse();
        result.Exports.Should().BeEmpty();
        result.Error.Should().Be("Error");
    }
}
