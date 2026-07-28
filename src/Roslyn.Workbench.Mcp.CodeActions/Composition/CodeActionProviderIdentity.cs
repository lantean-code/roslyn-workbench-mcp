namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal static class CodeActionProviderIdentity
{
    public static string GetId(CodeFixProvider provider)
    {
        return GetId(provider.GetType());
    }

    public static string GetId(CodeRefactoringProvider provider)
    {
        return GetId(provider.GetType());
    }

    private static string GetId(Type providerType)
    {
        return providerType.FullName ?? providerType.Name;
    }
}
