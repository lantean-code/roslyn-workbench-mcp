namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

internal static class CodeActionExecutionTestFactory
{
    public static InMemoryRoslynSolution CreateTwoProjectSolution()
    {
        return RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "FirstProject",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "First.cs", Source = "class First { }" },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "SecondProject",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "Second.cs", Source = "class Second { }" },
                ],
            },
        ]);
    }
}
