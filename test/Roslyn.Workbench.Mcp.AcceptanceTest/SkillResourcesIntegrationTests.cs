using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class SkillResourcesIntegrationTests
{
    [Theory]
    [InlineData("inspection-only", false)]
    [InlineData("transactional", false)]
    [InlineData("approval-required", false)]
    [InlineData("autonomous-trusted", false)]
    [InlineData("transactional", true)]
    [InlineData("approval-required", true)]
    [InlineData("autonomous-trusted", true)]
    public async Task GIVEN_PublishedHostWithoutOpenWorkspace_WHEN_ReadingSkillResources_THEN_ShouldServeCompleteOfflineSkill(
        string mode,
        bool compilerValidationRequired)
    {
        IReadOnlyList<string> arguments = compilerValidationRequired
            ? ["--commit-validation", "no-new-compiler-errors"]
            : [];

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: arguments,
            operationalMode: mode);

        try
        {
            target.ServerCapabilities.Resources.Should().NotBeNull();
            target.ServerInstructions.Should().Contain("skill://roslyn-workbench/SKILL.md");
            target.ServerInstructions!.Length.Should().BeLessThanOrEqualTo(512);
            var resources = await target.ListResourcesAsync(TestContext.Current.CancellationToken);
            resources.Select(resource => resource.Uri).Should().BeEquivalentTo(
                "skill://roslyn-workbench/SKILL.md",
                "skill://roslyn-workbench/references/workspaces.md",
                "skill://roslyn-workbench/references/selectors.md",
                "skill://roslyn-workbench/references/navigation.md",
                "skill://roslyn-workbench/references/structure.md",
                "skill://roslyn-workbench/references/diagnostics.md",
                "skill://roslyn-workbench/references/flow-analysis.md",
                "skill://roslyn-workbench/references/impact-analysis.md",
                "skill://roslyn-workbench/references/transactions.md",
                "skill://roslyn-workbench/references/code-actions.md",
                "skill://roslyn-workbench/references/rename-format.md",
                "skill://roslyn-workbench/references/recovery.md");

            foreach (var resource in resources)
            {
                resource.MimeType.Should().Be("text/markdown");
                resource.Description.Should().NotBeNullOrWhiteSpace();
                var result = await target.ReadResourceAsync(resource.Uri, TestContext.Current.CancellationToken);
                var content = result.Contents.Should().ContainSingle().Which.Should().BeOfType<TextResourceContents>().Subject;
                content.Uri.Should().Be(resource.Uri);
                content.MimeType.Should().Be("text/markdown");
                content.Text.Should().NotBeNullOrWhiteSpace();

                var relativePath = new Uri(resource.Uri).AbsolutePath.TrimStart('/');
                var canonicalPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Skill", relativePath);
                var canonicalContent = await File.ReadAllTextAsync(canonicalPath, TestContext.Current.CancellationToken);
                content.Text.Should().Be(canonicalContent);

                foreach (Match link in Regex.Matches(content.Text, @"\[[^\]]*\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    var linkedUri = new Uri(new Uri(resource.Uri), link.Groups[1].Value).AbsoluteUri;
                    resources.Select(item => item.Uri).Should().Contain(linkedUri);
                }
            }

            var readUnknown = () => target.ReadResourceAsync(
                "skill://roslyn-workbench/references/../../secret.md",
                TestContext.Current.CancellationToken);

            var exception = await readUnknown.Should().ThrowAsync<McpProtocolException>();
            exception.Which.ErrorCode.Should().Be(McpErrorCode.ResourceNotFound);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
