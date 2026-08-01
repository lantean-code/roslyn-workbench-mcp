using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class WorkspaceSelectorSchemaPublisher
{
    public static void Publish(JsonObject schema, Type contractType)
    {
        var constraint = contractType switch
        {
            _ when contractType == typeof(WorkspaceSelector) => CreateWorkspaceConstraint(),
            _ when contractType == typeof(ProjectSelector) => CreateProjectConstraint(),
            _ when contractType == typeof(DocumentSelector) => CreateDocumentConstraint(),
            _ when contractType == typeof(LocationSelector) => CreateLocationConstraint(),
            _ when contractType == typeof(SymbolSelector) => CreateSymbolConstraint(),
            _ when contractType == typeof(ScopeSelector) => CreateScopeConstraint(),
            _ => null,
        };

        if (constraint is null)
        {
            return;
        }

        var allOf = schema["allOf"] as JsonArray;
        if (allOf is null)
        {
            allOf = [];
            schema["allOf"] = allOf;
        }

        allOf.Add(constraint);
    }

    private static JsonObject CreateWorkspaceConstraint()
    {
        return CreateAnyOfConstraint(
            CreateRequiredPropertyBranch("workspaceId", CreateTypeSchema("string"), out _, out _),
            CreateRequiredPropertyBranch("alias", CreateMeaningfulStringSchema(), out _, out _),
            CreateRequiredPropertyBranch("path", CreateMeaningfulStringSchema(), out _, out _));
    }

    private static JsonObject CreateProjectConstraint()
    {
        return CreateAnyOfConstraint(
            CreateRequiredPropertyBranch("projectId", CreateMeaningfulStringSchema(), out _, out _),
            CreateRequiredPropertyBranch("name", CreateMeaningfulStringSchema(), out _, out _),
            CreateRequiredPropertyBranch("path", CreateMeaningfulStringSchema(), out _, out _),
            CreateRequiredPropertyBranch("targetFramework", CreateMeaningfulStringSchema(), out _, out _));
    }

    private static JsonObject CreateDocumentConstraint()
    {
        var pathBranch = CreateRequiredPropertyBranch(
            "path",
            CreateMeaningfulStringSchema(),
            out _,
            out var pathProperties);

        AddPropertyConstraint(pathProperties, "documentId", CreateNotMeaningfulStringSchema());

        var documentIdBranch = CreateRequiredPropertyBranch(
            "documentId",
            CreateMeaningfulStringSchema(),
            out _,
            out var documentIdProperties);

        AddPropertyConstraint(documentIdProperties, "path", CreateNotMeaningfulStringSchema());

        return CreateOneOfConstraint(pathBranch, documentIdBranch);
    }

    private static JsonObject CreateLocationConstraint()
    {
        var spanBranch = CreateRequiredPropertyBranch(
            "span",
            CreateTypeSchema("object"),
            out _,
            out var spanProperties);

        AddPropertyConstraint(spanProperties, "selection", CreateTypeSchema("null"));

        var selectionBranch = CreateRequiredPropertyBranch(
            "selection",
            CreateTypeSchema("object"),
            out _,
            out var selectionProperties);

        AddPropertyConstraint(selectionProperties, "span", CreateTypeSchema("null"));

        return CreateOneOfConstraint(spanBranch, selectionBranch);
    }

    private static JsonObject CreateSymbolConstraint()
    {
        var locationBranch = CreateRequiredPropertyBranch(
            "location",
            CreateTypeSchema("object"),
            out _,
            out var locationProperties);

        AddPropertyConstraint(locationProperties, "documentationCommentId", CreateNotMeaningfulStringSchema());

        var documentationIdBranch = CreateRequiredPropertyBranch(
            "documentationCommentId",
            CreateMeaningfulStringSchema(),
            out _,
            out var documentationIdProperties);

        AddPropertyConstraint(documentationIdProperties, "location", CreateTypeSchema("null"));
        return CreateOneOfConstraint(locationBranch, documentationIdBranch);
    }

    private static JsonObject CreateScopeConstraint()
    {
        var solutionBranch = CreateScopeBranch(
            ScopeKind.Solution,
            requireKind: false,
            out _,
            out var solutionProperties);
        AddPropertyConstraint(solutionProperties, "project", CreateTypeSchema("null"));
        AddPropertyConstraint(solutionProperties, "document", CreateTypeSchema("null"));
        AddPropertyConstraint(solutionProperties, "projects", CreateTypeSchema("null"));

        var projectBranch = CreateScopeBranch(
            ScopeKind.Project,
            requireKind: true,
            out var projectRequired,
            out var projectProperties);
        RequireProperty(projectRequired, "project");
        AddPropertyConstraint(projectProperties, "project", CreateTypeSchema("object"));
        AddPropertyConstraint(projectProperties, "document", CreateTypeSchema("null"));
        AddPropertyConstraint(projectProperties, "projects", CreateTypeSchema("null"));

        var documentBranch = CreateScopeBranch(
            ScopeKind.Document,
            requireKind: true,
            out var documentRequired,
            out var documentProperties);
        RequireProperty(documentRequired, "document");
        AddPropertyConstraint(documentProperties, "document", CreateTypeSchema("object"));
        AddPropertyConstraint(documentProperties, "project", CreateTypeSchema("null"));
        AddPropertyConstraint(documentProperties, "projects", CreateTypeSchema("null"));

        var projectsSchema = CreateTypeSchema("array");
        projectsSchema["minItems"] = 1;
        var projectsBranch = CreateScopeBranch(
            ScopeKind.Projects,
            requireKind: true,
            out var projectsRequired,
            out var projectsProperties);
        RequireProperty(projectsRequired, "projects");
        AddPropertyConstraint(projectsProperties, "projects", projectsSchema);
        AddPropertyConstraint(projectsProperties, "project", CreateTypeSchema("null"));
        AddPropertyConstraint(projectsProperties, "document", CreateTypeSchema("null"));

        return CreateOneOfConstraint(solutionBranch, projectBranch, documentBranch, projectsBranch);
    }

    private static JsonObject CreateScopeBranch(
        ScopeKind kind,
        bool requireKind,
        out JsonArray required,
        out JsonObject properties)
    {
        required = [];
        properties = new JsonObject
        {
            ["kind"] = new JsonObject
            {
                ["const"] = kind.ToString(),
            },
        };

        var branch = new JsonObject
        {
            ["properties"] = properties,
        };

        if (requireKind)
        {
            RequireProperty(required, "kind");
            branch["required"] = required;
        }

        return branch;
    }

    private static JsonObject CreateAnyOfConstraint(params JsonObject[] branches)
    {
        return new JsonObject
        {
            ["anyOf"] = new JsonArray(branches),
        };
    }

    private static JsonObject CreateOneOfConstraint(params JsonObject[] branches)
    {
        return new JsonObject
        {
            ["oneOf"] = new JsonArray(branches),
        };
    }

    private static JsonObject CreateRequiredPropertyBranch(
        string propertyName,
        JsonObject propertySchema,
        out JsonArray required,
        out JsonObject properties)
    {
        required = new JsonArray(propertyName);
        properties = new JsonObject();
        var branch = new JsonObject
        {
            ["required"] = required,
            ["properties"] = properties,
        };

        AddPropertyConstraint(properties, propertyName, propertySchema);
        return branch;
    }

    private static void RequireProperty(JsonArray required, string propertyName)
    {
        required.Add(propertyName);
    }

    private static void AddPropertyConstraint(
        JsonObject properties,
        string propertyName,
        JsonObject propertySchema)
    {
        properties[propertyName] = propertySchema;
    }

    private static JsonObject CreateMeaningfulStringSchema()
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["pattern"] = @"\S",
        };
    }

    private static JsonObject CreateNotMeaningfulStringSchema()
    {
        return new JsonObject
        {
            ["not"] = CreateMeaningfulStringSchema(),
        };
    }

    private static JsonObject CreateTypeSchema(string type)
    {
        return new JsonObject
        {
            ["type"] = type,
        };
    }
}
