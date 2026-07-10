namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal delegate CodeActionDescriptorEntry? CodeActionDescriptorOverride(
    CodeAction action,
    string providerId,
    string title);
