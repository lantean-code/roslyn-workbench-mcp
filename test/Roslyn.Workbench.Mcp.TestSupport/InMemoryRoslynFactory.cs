using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Creates narrow in-memory Roslyn objects for unit tests that require real workspace state.
/// </summary>
public static class InMemoryRoslynFactory
{
    /// <summary>
    /// Creates a single-document in-memory C# Roslyn model.
    /// </summary>
    /// <param name="source">The C# source text to load into the document.</param>
    /// <param name="documentName">The logical document name.</param>
    /// <returns>The created in-memory document wrapper.</returns>
    public static InMemoryRoslynDocument CreateDocument(string source, string documentName = "Code.cs")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        var solution = CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = documentName,
                        Source = source,
                    },
                ],
            },
        ]);

        var document = solution.GetDocument(documentName);
        return new InMemoryRoslynDocument(solution.Workspace, solution.Solution, document);
    }

    /// <summary>
    /// Creates an in-memory Roslyn solution from the supplied project definitions.
    /// </summary>
    /// <param name="projects">The projects to include in the solution.</param>
    /// <returns>The created in-memory solution wrapper.</returns>
    public static InMemoryRoslynSolution CreateSolution(IReadOnlyList<InMemoryRoslynProjectDefinition> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        if (projects.Count == 0)
        {
            throw new ArgumentException("At least one project is required.", nameof(projects));
        }

        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var versionStamp = VersionStamp.Create();
        var projectIdsByName = new Dictionary<string, ProjectId>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            ValidateProjectDefinition(project);

            if (!projectIdsByName.TryAdd(project.Name, ProjectId.CreateNewId(project.Name)))
            {
                throw new InvalidOperationException($"The project '{project.Name}' was specified more than once.");
            }
        }

        foreach (var project in projects)
        {
            var projectId = projectIdsByName[project.Name];
            var projectFilePath = project.FilePath ?? $"/workspace/{project.Name}/{project.Name}.csproj";

            solution = solution.AddProject(ProjectInfo.Create(
                projectId,
                versionStamp,
                project.Name,
                project.AssemblyName ?? project.Name,
                LanguageNames.CSharp,
                filePath: projectFilePath,
                metadataReferences: CreateMetadataReferences(),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.Preview)));
        }

        foreach (var project in projects)
        {
            var projectId = projectIdsByName[project.Name];

            foreach (var referencedProjectName in project.ProjectReferences)
            {
                if (!projectIdsByName.TryGetValue(referencedProjectName, out var referencedProjectId))
                {
                    throw new InvalidOperationException($"The project '{project.Name}' references unknown project '{referencedProjectName}'.");
                }

                solution = solution.AddProjectReference(projectId, new ProjectReference(referencedProjectId));
            }

            foreach (var document in project.Documents)
            {
                var documentId = DocumentId.CreateNewId(projectId, document.Name);
                var documentFilePath = document.FilePath ?? $"/workspace/{project.Name}/{document.Name}";

                solution = solution.AddDocument(DocumentInfo.Create(
                    documentId,
                    document.Name,
                    filePath: documentFilePath,
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(document.Source), versionStamp))));
            }
        }

        workspace.TryApplyChanges(solution);

        return new InMemoryRoslynSolution(
            workspace,
            workspace.CurrentSolution,
            projectIdsByName.ToImmutableDictionary(StringComparer.Ordinal));
    }

    private static void ValidateProjectDefinition(InMemoryRoslynProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Name);
        ArgumentNullException.ThrowIfNull(project.Documents);

        if (project.Documents.Count == 0)
        {
            throw new ArgumentException($"The project '{project.Name}' must contain at least one document.", nameof(project));
        }

        var documentNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in project.Documents)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(document.Name);
            ArgumentNullException.ThrowIfNull(document.Source);

            if (!documentNames.Add(document.Name))
            {
                throw new InvalidOperationException($"The project '{project.Name}' contains duplicate document '{document.Name}'.");
            }
        }
    }

    private static IReadOnlyList<MetadataReference> CreateMetadataReferences()
    {
        var assemblyLocations = new[]
        {
            typeof(object).Assembly.Location,
            typeof(Enumerable).Assembly.Location,
            typeof(Console).Assembly.Location,
            typeof(System.Runtime.GCSettings).Assembly.Location,
        };

        return assemblyLocations
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static item => MetadataReference.CreateFromFile(item))
            .ToArray();
    }
}
