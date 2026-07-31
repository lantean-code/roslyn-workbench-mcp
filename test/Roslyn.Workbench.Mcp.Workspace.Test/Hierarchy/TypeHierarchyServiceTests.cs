using Roslyn.Workbench.Mcp.Workspace.Hierarchy;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Hierarchy;

public sealed class TypeHierarchyServiceTests
{
    private readonly TypeHierarchyService _target = new();

    [Fact]
    public async Task GIVEN_ClassHierarchy_WHEN_FindingDerivedTypes_THEN_ShouldReturnShortestDepths()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Hierarchy.cs",
                        Source = """
                            class Root { }
                            class Direct : Root { }
                            class Indirect : Direct { }
                            """,
                    },
                ],
            },
        ]);
        var root = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Hierarchy.cs"),
            "Root",
            TestContext.Current.CancellationToken);

        var result = await _target.FindDerivedTypesAsync(
            root,
            solution.Solution,
            solution.Solution.Projects.ToArray(),
            TestContext.Current.CancellationToken);

        result.ToDictionary(static item => item.Type.Name, static item => item.Depth).Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                ["Direct"] = 1,
                ["Indirect"] = 2,
            });
    }

    [Fact]
    public async Task GIVEN_InterfaceHierarchy_WHEN_FindingDerivedTypes_THEN_ShouldIncludeDerivedInterfacesAndImplementations()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Hierarchy.cs",
                        Source = """
                            interface IRoot { }
                            interface IChild : IRoot { }
                            class Direct : IRoot { }
                            class Indirect : IChild { }
                            """,
                    },
                ],
            },
        ]);
        var root = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Hierarchy.cs"),
            "IRoot",
            TestContext.Current.CancellationToken);

        var result = await _target.FindDerivedTypesAsync(
            root,
            solution.Solution,
            solution.Solution.Projects.ToArray(),
            TestContext.Current.CancellationToken);

        result.ToDictionary(static item => item.Type.Name, static item => item.Depth).Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                ["IChild"] = 1,
                ["Direct"] = 1,
                ["Indirect"] = 2,
            });
    }
}
