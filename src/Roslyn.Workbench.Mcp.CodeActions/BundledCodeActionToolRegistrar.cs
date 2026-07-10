using Roslyn.Workbench.Mcp.CodeActions.Refactorings;
using Roslyn.Workbench.Mcp.CodeActions.Tools;

namespace Roslyn.Workbench.Mcp.CodeActions;

internal static class BundledCodeActionToolRegistrar
{
    public static void RegisterAll(ICodeActionToolRegistry registry)
    {
        RegisterBuiltInCodeActionTool(registry, "add-debugger-display", AddDebuggerDisplayTool.Register);
        RegisterBuiltInCodeActionTool(registry, "add-import", AddImportTool.Register);
        RegisterBuiltInCodeActionTool(registry, "add-null-checks", AddNullChecksTool.Register);
        RegisterBuiltInCodeActionTool(registry, "extract-method", ExtractMethodTool.Register);
        RegisterBuiltInCodeActionTool(registry, "add-await", AddAwaitTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-anonymous-type-to-class", ConvertAnonymousTypeToClassTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-anonymous-type-to-tuple", ConvertAnonymousTypeToTupleTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-auto-property-to-full-property", ConvertAutoPropertyToFullPropertyTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-between-regular-and-verbatim-interpolated-string", ConvertBetweenRegularAndVerbatimInterpolatedStringTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-between-regular-and-verbatim-string", ConvertBetweenRegularAndVerbatimStringTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-direct-cast-to-try-cast", ConvertDirectCastToTryCastTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-expression-body", ConvertExpressionBodyTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-foreach-to-for", ConvertForEachToForTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-for-to-foreach", ConvertForToForeachTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-if-to-switch", ConvertIfToSwitchTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-local-function-to-method", ConvertLocalFunctionToMethodTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-primary-to-regular-constructor", ConvertPrimaryToRegularConstructorTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-to-record", ConvertToRecordTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-try-cast-to-direct-cast", ConvertTryCastToDirectCastTool.Register);
        RegisterBuiltInCodeActionTool(registry, "invert-conditional", InvertConditionalTool.Register);
        RegisterBuiltInCodeActionTool(registry, "invert-if", InvertIfTool.Register);
        RegisterBuiltInCodeActionTool(registry, "invert-logical", InvertLogicalTool.Register);
        RegisterBuiltInCodeActionTool(registry, "move-declaration-near-reference", MoveDeclarationNearReferenceTool.Register);
        RegisterBuiltInCodeActionTool(registry, "move-type-to-file", MoveTypeToFileTool.Register);
        RegisterBuiltInCodeActionTool(registry, "name-tuple-element", NameTupleElementTool.Register);
        RegisterBuiltInCodeActionTool(registry, "replace-conditional-with-statements", ReplaceConditionalWithStatementsTool.Register);
        RegisterBuiltInCodeActionTool(registry, "replace-doc-comment-text-with-tag", ReplaceDocCommentTextWithTagTool.Register);
        RegisterBuiltInCodeActionTool(registry, "reverse-for-statement", ReverseForStatementTool.Register);
        RegisterBuiltInCodeActionTool(registry, "introduce-using-statement", IntroduceUsingStatementTool.Register);
        RegisterBuiltInCodeActionTool(registry, "introduce-parameter", IntroduceParameterTool.Register);
        RegisterBuiltInCodeActionTool(registry, "encapsulate-field", EncapsulateFieldTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-foreach-linq", ConvertForeachLinqTool.Register);
        RegisterBuiltInCodeActionTool(registry, "introduce-variable", IntroduceVariableTool.Register);
        RegisterBuiltInCodeActionTool(registry, "convert-to-interpolated-string", ConvertToInterpolatedStringTool.Register);
        RegisterBuiltInCodeActionTool(registry, "use-explicit-type", UseExplicitTypeTool.Register);
        RegisterBuiltInCodeActionTool(registry, "use-implicit-type", UseImplicitTypeTool.Register);
        RegisterBuiltInCodeActionTool(registry, "use-recursive-patterns", UseRecursivePatternsTool.Register);
        RegisterBuiltInCodeActionTool(registry, "use-named-arguments", UseNamedArgumentsTool.Register);
        RegisterBuiltInCodeActionTool(registry, "make-local-function-static", MakeLocalFunctionStaticTool.Register);
        RegisterBuiltInCodeActionTool(registry, "inline-variable", InlineVariableTool.Register);
        ConvertPropertyTool.Register(registry);
        RegisterBuiltInCodeActionTool(registry, "add-missing-usings", AddMissingUsingsTool.Register);
        RegisterBuiltInCodeActionTool(registry, "remove-unused-usings", RemoveUnusedUsingsTool.Register);
        ListCodeActionsTool.Register(registry);
        DescribeCodeActionTool.Register(registry);
        StageCodeActionTool.Register(registry);
        StageCodeFixTool.Register(registry);
        StageFixAllTool.Register(registry);
    }

    private static void RegisterBuiltInCodeActionTool(ICodeActionToolRegistry registry, string toolName, Action<ICodeActionToolRegistry> register)
    {
        if (BuiltInCodeActionLedger.IsDedicatedToolVisible(toolName))
        {
            register(registry);
        }
    }
}
