namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class CodeActionAcceptanceCases
{
    public static IReadOnlyList<CodeActionAcceptanceCase> Create(string workspaceRoot)
    {
        var locations = new AcceptanceLocationSelectorFactory(workspaceRoot);
        var cases = new List<CodeActionAcceptanceCase>();

        AddCompilerCodeFixCases(cases, locations);
        AddReplayRefactoringCases(cases, locations);
        AddCustomWorkflowCases(cases, locations);

        return cases;
    }

    private static void AddCompilerCodeFixCases(
        List<CodeActionAcceptanceCase> cases,
        AcceptanceLocationSelectorFactory locations)
    {
        const string documentPath = "CandidateCodeFixes.cs";

        cases.Add(CreateCompilerCodeFixCase(
            "add-anonymous-type-member-name",
            "CS0746",
            "location",
            locations.CreateLocation(documentPath, "value + 1"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-conditional-interpolation-parentheses",
            "CS8361",
            "location",
            locations.CreateLocation(documentPath, "enabled ? \"enabled\" : \"disabled\""),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-explicit-cast",
            "CS0266",
            "location",
            locations.CreateLocation(documentPath, "value;", occurrenceIndex: 0),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-inheritdoc",
            "CS1591",
            "location",
            locations.CreateLocation(documentPath, "DocumentedMember", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-obsolete-attribute",
            "CS0612",
            "location",
            locations.CreateLocation(documentPath, "CandidateObsoleteBase", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-obsolete-attribute",
            "CS0618",
            "location",
            locations.CreateLocation(documentPath, "CandidateObsoleteMessageBase", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-obsolete-attribute",
            "CS0672",
            "location",
            locations.CreateLocation(documentPath, "override void ObsoleteOverride()"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-obsolete-attribute",
            "CS1062",
            "location",
            locations.CreateLocation(documentPath, "{ 1 }", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-obsolete-attribute",
            "CS1064",
            "location",
            locations.CreateLocation(documentPath, "{ 1 }", occurrenceIndex: 0),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-yield",
            "CS0029",
            "location",
            locations.CreateLocation(documentPath, "return \"value\";"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "add-yield",
            "CS0266",
            "location",
            locations.CreateLocation(documentPath, "return new object();"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "assign-out-parameters",
            "CS0177",
            "location",
            locations.CreateLocation(documentPath, "return 'a';", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "change-iterator-return-type",
            "CS1624",
            "location",
            locations.CreateLocation(documentPath, "ChangeIteratorReturnType"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "declare-as-nullable",
            "CS8603",
            "location",
            locations.CreateLocation(documentPath, "null"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "declare-as-nullable",
            "CS8600",
            "location",
            locations.CreateLocation(documentPath, "string value = null;"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "declare-as-nullable",
            "CS8625",
            "location",
            locations.CreateLocation(documentPath, "AcceptNullableValue(null)"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "declare-as-nullable",
            "CS8618",
            "location",
            locations.CreateLocation(documentPath, "Value { get; }"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "fix-incorrect-constraint",
            "CS9010",
            "location",
            locations.CreateLocation(documentPath, "enum"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "fix-incorrect-constraint",
            "CS9011",
            "location",
            locations.CreateLocation(documentPath, "delegate"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "fix-return-type",
            "CS0127",
            "location",
            locations.CreateLocation(documentPath, "return 1;"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "fix-return-type",
            "CS1997",
            "location",
            locations.CreateLocation(documentPath, "return 1;", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "fix-return-type",
            "CS0201",
            "location",
            locations.CreateLocation(documentPath, "=> 1;"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "hide-base-member",
            "CS0108",
            "location",
            locations.CreateLocation(documentPath, "HiddenMember", occurrenceIndex: 1),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "make-member-required",
            "CS8618",
            "location",
            locations.CreateLocation(documentPath, "RequiredValue"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "order-modifiers",
            "CS0267",
            "location",
            locations.CreateLocation(documentPath, "partial public"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "remove-in-keyword",
            "CS1615",
            "location",
            locations.CreateLocation(documentPath, "in value"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "remove-new-modifier",
            "CS0109",
            "location",
            locations.CreateLocation(documentPath, "internal new void RemoveNewModifier"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "remove-unused-local-function",
            "CS8321",
            "location",
            locations.CreateLocation(documentPath, "UnusedLocalFunction"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "replace-default-literal",
            "CS8505",
            "location",
            locations.CreateLocation(documentPath, "default"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "transpose-record-keyword",
            "CS9012",
            "location",
            locations.CreateLocation(documentPath, "record"),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "use-explicit-array-in-expression-tree",
            "CS9226",
            "location",
            locations.CreateLocation(documentPath, "Format()", occurrenceIndex: 0),
            documentPath));

        cases.Add(CreateCompilerCodeFixCase(
            "use-explicit-type-for-const",
            "CS0822",
            "location",
            locations.CreateLocation(documentPath, "const var"),
            documentPath));
    }

    private static void AddReplayRefactoringCases(
        List<CodeActionAcceptanceCase> cases,
        AcceptanceLocationSelectorFactory locations)
    {
        AddCandidateRefactoringCases(cases, locations);
        AddFormattingRefactoringCases(cases, locations);

        cases.Add(CreateTargetCase(
            "add-null-checks",
            "selection",
            locations.CreateCursor("AddParameterCheck.cs", "object value"),
            "AddParameterCheck.cs"));
    }

    private static void AddCandidateRefactoringCases(
        List<CodeActionAcceptanceCase> cases,
        AcceptanceLocationSelectorFactory locations)
    {
        const string documentPath = "CandidateRefactorings.cs";
        const int requiredConstructorParameters = 0;
        const int getterAndSetter = 1;

        cases.Add(CreateTargetCase(
            "add-constructor-parameters",
            "members",
            locations.CreateSelection(
                documentPath,
                "private readonly int _count;",
                "private readonly string _name;"),
            documentPath,
            ("kind", requiredConstructorParameters)));

        cases.Add(CreateTargetCase(
            "generate-comparison-operators",
            "selection",
            locations.CreateCursor(documentPath, "ComparisonOperatorCandidate"),
            documentPath));

        cases.Add(CreateTargetCase(
            "implement-interface",
            "selection",
            locations.CreateCursorInsideTypeBody(
                documentPath,
                "InterfaceImplementationCandidate : ICandidateFormatter"),
            documentPath));

        cases.Add(CreateTargetCase(
            "organize-imports",
            "document",
            AcceptanceLocationSelectorFactory.CreateDocument(documentPath),
            documentPath));

        cases.Add(CreateTargetCase(
            "replace-method-with-property",
            "method",
            locations.CreateCursor(documentPath, "GetValue"),
            documentPath,
            ("kind", getterAndSetter)));

        cases.Add(CreateTargetCase(
            "replace-property-with-methods",
            "selection",
            locations.CreateCursor(
                documentPath,
                "public int Value { get; set; }",
                offset: "public int ".Length),
            documentPath));
    }

    private static void AddFormattingRefactoringCases(
        List<CodeActionAcceptanceCase> cases,
        AcceptanceLocationSelectorFactory locations)
    {
        const string documentPath = "Formatting.cs";

        cases.Add(CreateTargetCase(
            "convert-between-regular-and-verbatim-interpolated-string",
            "selection",
            locations.CreateLocation(documentPath, "$\"C:\\\\temp\\\\{value}\""),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-between-regular-and-verbatim-string",
            "selection",
            locations.CreateLocation(documentPath, "\"C:\\\\temp\\\\logs\""),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-foreach-to-for",
            "selection",
            locations.CreateLocation(documentPath, "foreach (var value in values)"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-for-to-foreach",
            "selection",
            locations.CreateLocation(documentPath, "for (var i = 0; i < values.Length; i++)"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-anonymous-type-to-tuple",
            "selection",
            locations.CreateLocation(documentPath, "new { Name = \"Alpha\", Count = 1 }"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-anonymous-type-to-class",
            "selection",
            locations.CreateLocation(documentPath, "new { Name = \"Alpha\", Count = 1 }"),
            documentPath,
            ("kind", 0)));

        cases.Add(CreateTargetCase(
            "convert-auto-property-to-full-property",
            "selection",
            locations.CreateLocation(documentPath, "Goo"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-to-record",
            "selection",
            locations.CreateLocation(documentPath, "ConvertibleToRecord"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-direct-cast-to-try-cast",
            "selection",
            locations.CreateLocation(documentPath, "(object)1"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-expression-body",
            "selection",
            locations.CreateLocation(documentPath, "Square"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-local-function-to-method",
            "selection",
            locations.CreateLocation(documentPath, "Local"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-primary-to-regular-constructor",
            "selection",
            locations.CreateLocation(documentPath, "PrimaryConstructorSamples(int value)"),
            documentPath));

        cases.Add(CreateTargetCase(
            "convert-try-cast-to-direct-cast",
            "selection",
            locations.CreateLocation(documentPath, "value as string"),
            documentPath));

        cases.Add(CreateTargetCase(
            "invert-conditional",
            "selection",
            locations.CreateLocation(documentPath, "count == 0 ? \"zero\" : \"non-zero\""),
            documentPath));

        cases.Add(CreateTargetCase(
            "invert-if",
            "selection",
            locations.CreateLocation(documentPath, "if (left > 0)"),
            documentPath));

        cases.Add(CreateTargetCase(
            "make-local-function-static",
            "selection",
            locations.CreateLocation(documentPath, "Local"),
            documentPath));

        cases.Add(CreateTargetCase(
            "move-declaration-near-reference",
            "selection",
            locations.CreateLocation(documentPath, "int moved;"),
            documentPath));

        cases.Add(CreateTargetCase(
            "name-tuple-element",
            "selection",
            locations.CreateCursor(documentPath, "return (1 + 1, 2);", offset: "return (".Length),
            documentPath));

        cases.Add(CreateTargetCase(
            "replace-doc-comment-text-with-tag",
            "selection",
            locations.CreateLocation(documentPath, "System.IDisposable"),
            documentPath));

        cases.Add(CreateTargetCase(
            "reverse-for-statement",
            "selection",
            locations.CreateLocation(documentPath, "for (var i = 0; i < values.Length; i++)"),
            documentPath));

        cases.Add(CreateTargetCase(
            "use-explicit-type",
            "selection",
            locations.CreateCursor(documentPath, "var explicitBuilder", offset: "var".Length),
            documentPath));

        cases.Add(CreateTargetCase(
            "use-implicit-type",
            "selection",
            locations.CreateCursor(documentPath, "StringBuilder implicitBuilder", offset: "StringBuilder".Length),
            documentPath));

        cases.Add(CreateTargetCase(
            "use-named-arguments",
            "selection",
            locations.CreateCursor(documentPath, "Sum(1, 2)", offset: 4),
            documentPath,
            ("includeTrailingArguments", false)));

        cases.Add(CreateTargetCase(
            "use-recursive-patterns",
            "selection",
            locations.CreateCursor(
                documentPath,
                "cf != null && cf.C != 0",
                offset: "cf != null ".Length),
            documentPath));

        cases.Add(CreateTargetCase(
            "add-await",
            "selection",
            locations.CreateCursorAfter(documentPath, "GetValueAsync()", occurrenceIndex: 2),
            documentPath,
            ("kind", 0)));

        cases.Add(CreateTargetCase(
            "add-debugger-display",
            "selection",
            locations.CreateLocation(documentPath, "GreetingFormatter"),
            documentPath));

        cases.Add(CreateTargetCase(
            "add-import",
            "selection",
            locations.CreateCursor(documentPath, "System.Net.Http.HttpClient"),
            documentPath,
            ("simplifyAllOccurrences", false)));

        cases.Add(CreateTargetCase(
            "convert-if-to-switch",
            "selection",
            locations.CreateLocation(documentPath, "if (value == 0)"),
            documentPath,
            ("kind", 0)));

        cases.Add(CreateTargetCase(
            "invert-logical",
            "selection",
            locations.CreateLocation(documentPath, "&&"),
            documentPath));

        cases.Add(CreateTargetCase(
            "introduce-using-statement",
            "selection",
            locations.CreateLocation(documentPath, "var stream = new MemoryStream();"),
            documentPath));

        cases.Add(CreateTargetCase(
            "replace-conditional-with-statements",
            "selection",
            locations.CreateLocation(documentPath, "value = enabled ? 1 : 2;"),
            documentPath));
    }

    private static void AddCustomWorkflowCases(
        List<CodeActionAcceptanceCase> cases,
        AcceptanceLocationSelectorFactory locations)
    {
        AddScopedUsingCases(cases);
        AddCustomFormattingCases(cases, locations);
    }

    private static void AddScopedUsingCases(List<CodeActionAcceptanceCase> cases)
    {
        var missingUsingsScope = CreateDocumentScope("MissingUsings.cs");
        var addMissingArguments = new Dictionary<string, object?>
        {
            ["scope"] = missingUsingsScope,
            ["preferGlobalUsings"] = false,
        };

        cases.Add(new CodeActionAcceptanceCase(
            "add-missing-usings",
            addMissingArguments,
            ["MissingUsings.cs"]));

        var removeUnusedArguments = new Dictionary<string, object?>
        {
            ["scope"] = CreateDocumentScope("Usings.cs"),
        };

        cases.Add(new CodeActionAcceptanceCase(
            "remove-unused-usings",
            removeUnusedArguments,
            ["Usings.cs"]));
    }

    private static void AddCustomFormattingCases(
        List<CodeActionAcceptanceCase> cases,
        AcceptanceLocationSelectorFactory locations)
    {
        const string documentPath = "Formatting.cs";

        cases.Add(CreateTargetCase(
            "convert-foreach-linq",
            "selection",
            locations.CreateLocation(documentPath, "foreach (var number in numbers)"),
            documentPath,
            ("conversionKind", 0)));

        cases.Add(CreateTargetCase(
            "convert-property",
            "selection",
            locations.CreateLocation(documentPath, "Goo"),
            documentPath,
            ("direction", 0)));

        cases.Add(CreateTargetCase(
            "convert-to-interpolated-string",
            "selection",
            locations.CreateLocation(documentPath, "formatted + \"!\""),
            documentPath));

        cases.Add(CreateTargetCase(
            "encapsulate-field",
            "field",
            AcceptanceLocationSelectorFactory.CreateSymbol("F:Sample.FieldHolder._backingField"),
            documentPath,
            ("updateReferences", true)));

        cases.Add(CreateTargetCase(
            "extract-method",
            "selection",
            locations.CreateSelection(
                documentPath,
                "var adjusted = value + 1;",
                "adjusted *= 2;"),
            documentPath,
            ("targetKind", 0)));

        cases.Add(CreateTargetCase(
            "inline-variable",
            "symbol",
            AcceptanceLocationSelectorFactory.CreateSymbol(
                locations.CreateLocation(documentPath, "adjusted", occurrenceIndex: 0)),
            documentPath,
            ("removeDeclaration", true)));

        cases.Add(CreateTargetCase(
            "introduce-parameter",
            "selection",
            locations.CreateLocation(documentPath, "value + 1", occurrenceIndex: 0),
            documentPath,
            ("allOccurrences", false),
            ("strategy", 0)));

        cases.Add(CreateTargetCase(
            "introduce-variable",
            "selection",
            locations.CreateLocation(documentPath, "value + 1", occurrenceIndex: 0),
            documentPath,
            ("kind", 0)));

        cases.Add(CreateTargetCase(
            "move-type-to-file",
            "type",
            AcceptanceLocationSelectorFactory.CreateSymbol("T:Sample.AlphaCycle"),
            [documentPath, "AlphaCycle.cs"],
            ("preserveNamespace", true)));
    }

    private static CodeActionAcceptanceCase CreateTargetCase(
        string toolName,
        string targetName,
        Dictionary<string, object?> target,
        string expectedDocumentPath,
        params (string Name, object? Value)[] additionalArguments)
    {
        return CreateTargetCase(
            toolName,
            targetName,
            target,
            [expectedDocumentPath],
            additionalArguments);
    }

    private static CodeActionAcceptanceCase CreateTargetCase(
        string toolName,
        string targetName,
        Dictionary<string, object?> target,
        IReadOnlyList<string> expectedDocumentPaths,
        params (string Name, object? Value)[] additionalArguments)
    {
        var arguments = new Dictionary<string, object?>
        {
            [targetName] = target,
        };

        foreach (var (name, value) in additionalArguments)
        {
            arguments.Add(name, value);
        }

        return new CodeActionAcceptanceCase(toolName, arguments, expectedDocumentPaths);
    }

    private static CodeActionAcceptanceCase CreateCompilerCodeFixCase(
        string toolName,
        string diagnosticId,
        string targetName,
        Dictionary<string, object?> target,
        string expectedDocumentPath)
    {
        var arguments = new Dictionary<string, object?>
        {
            [targetName] = target,
        };

        return new CodeActionAcceptanceCase(
            toolName,
            arguments,
            [expectedDocumentPath],
            diagnosticId);
    }

    private static Dictionary<string, object?> CreateDocumentScope(string documentPath)
    {
        return new Dictionary<string, object?>
        {
            ["kind"] = "Document",
            ["document"] = AcceptanceLocationSelectorFactory.CreateDocument(documentPath),
        };
    }
}
