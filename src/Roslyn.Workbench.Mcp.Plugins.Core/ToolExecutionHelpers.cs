using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class ToolExecutionHelpers
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public static PluginExecutionResult<T> EnsureWithinSize<T>(IQueryContext context, T data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _serializerOptions);
        return bytes.Length > context.MaxResponseBytes
            ? Rejected<T>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest)
            : PluginExecutionResult<T>.Success(data);
    }

    public static int GetMaxResults(IQueryContext context, ResultLimit? requestLimit)
    {
        return requestLimit?.MaxResults ?? context.EffectiveResultLimit.MaxResults ?? 100;
    }

    public static PluginExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetName}Ambiguous", $"The {targetName.ToLowerInvariant()} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetName}NotFound", $"The {targetName.ToLowerInvariant()} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    public static PluginExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    public static async ValueTask<string?> ReadContextAsync(Document? document, TextSpan span, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return null;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(span.Start);
        return line.ToString().Trim();
    }

    public static ResolutionResult<Document, T> ResolveDocument<T>(DocumentSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            return new ResolutionResult<Document, T>
            {
                Rejection = Rejected<T>("InvalidRequest", "A document selector is required."),
            };
        }

        var resolution = context.Resolver.ResolveDocument(selector);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new ResolutionResult<Document, T> { Value = resolution.Value! }
            : new ResolutionResult<Document, T> { Rejection = RejectFromStatus<T>(resolution.Status, "Document") };
    }

    public static ResolutionResult<Project, T> ResolveProject<T>(ProjectSelector? selector, IToolExecutionContext context)
    {
        if (selector is null)
        {
            return new ResolutionResult<Project, T>
            {
                Rejection = Rejected<T>("InvalidRequest", "A project selector is required."),
            };
        }

        var resolution = context.Resolver.ResolveProject(selector);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new ResolutionResult<Project, T> { Value = resolution.Value! }
            : new ResolutionResult<Project, T> { Rejection = RejectFromStatus<T>(resolution.Status, "Project") };
    }

    public static ResolutionResult<IReadOnlyList<Document>, T> ResolveDocuments<T>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            return new ResolutionResult<IReadOnlyList<Document>, T>
            {
                Value = context.CurrentSolution.Projects.SelectMany(static project => project.Documents).ToArray(),
            };
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<T>(scope.Document, context);
            return documentResolution.HasRejection
                ? new ResolutionResult<IReadOnlyList<Document>, T> { Rejection = documentResolution.Rejection }
                : new ResolutionResult<IReadOnlyList<Document>, T> { Value = [documentResolution.Value] };
        }

        var projects = ResolveProjects<T>(scope, context);
        return projects.HasRejection
            ? new ResolutionResult<IReadOnlyList<Document>, T> { Rejection = projects.Rejection }
            : new ResolutionResult<IReadOnlyList<Document>, T>
            {
                Value = projects.Value
                    .SelectMany(static project => project.Documents)
                    .ToArray(),
            };
    }

    public static ResolutionResult<IReadOnlyList<Project>, T> ResolveProjects<T>(ScopeSelector? scope, IToolExecutionContext context)
    {
        if (scope is null || scope.Kind == ScopeKind.Solution)
        {
            return new ResolutionResult<IReadOnlyList<Project>, T>
            {
                Value = context.CurrentSolution.Projects.OrderBy(static project => project.Name, StringComparer.Ordinal).ToArray(),
            };
        }

        if (scope.Kind == ScopeKind.Document)
        {
            var documentResolution = ResolveDocument<T>(scope.Document, context);
            return documentResolution.HasRejection
                ? new ResolutionResult<IReadOnlyList<Project>, T> { Rejection = documentResolution.Rejection }
                : new ResolutionResult<IReadOnlyList<Project>, T> { Value = [documentResolution.Value.Project] };
        }

        if (scope.Kind == ScopeKind.Project)
        {
            var projectResolution = ResolveProject<T>(scope.Project, context);
            return projectResolution.HasRejection
                ? new ResolutionResult<IReadOnlyList<Project>, T> { Rejection = projectResolution.Rejection }
                : new ResolutionResult<IReadOnlyList<Project>, T> { Value = [projectResolution.Value] };
        }

        var projects = new List<Project>();
        foreach (var selector in scope.Projects ?? [])
        {
            var projectResolution = ResolveProject<T>(selector, context);
            if (projectResolution.HasRejection)
            {
                return new ResolutionResult<IReadOnlyList<Project>, T> { Rejection = projectResolution.Rejection };
            }

            projects.Add(projectResolution.Value);
        }

        return new ResolutionResult<IReadOnlyList<Project>, T>
        {
            Value = projects
                .DistinctBy(static project => project.Id)
                .OrderBy(static project => project.Name, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    public static ValueTask<PluginExecutionResult<MutationProposal>> StageReplaySelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        IMutationContext context,
        CancellationToken cancellationToken,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null)
    {
        if (selection is null)
        {
            return ValueTask.FromResult(Rejected<MutationProposal>("InvalidRequest", "A location selector is required."));
        }

        return context.CodeActionService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = selection,
            ExpectedSnapshot = expectedSnapshot,
            ProviderId = providerId,
            Title = title,
            TitleStartsWith = titleStartsWith,
            TitleDoesNotContain = titleDoesNotContain,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        }, context, cancellationToken);
    }

    public static async ValueTask<ResolutionResult<ISymbol, T>> ResolveSymbolAsync<T>(SymbolSelector? selector, SnapshotPrecondition? expectedSnapshot, IToolExecutionContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = selector?.Location is not null ? ValidateSnapshot<T>(context, expectedSnapshot) : null;
        if (snapshotRejection is not null)
        {
            return new ResolutionResult<ISymbol, T>
            {
                Rejection = snapshotRejection,
            };
        }

        if (selector is null)
        {
            return new ResolutionResult<ISymbol, T>
            {
                Rejection = Rejected<T>("InvalidRequest", "A symbol selector is required."),
            };
        }

        var resolution = await context.Resolver.ResolveSymbolAsync(selector, cancellationToken).ConfigureAwait(false);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new ResolutionResult<ISymbol, T> { Value = resolution.Value! }
            : new ResolutionResult<ISymbol, T> { Rejection = RejectFromStatus<T>(resolution.Status, "Symbol") };
    }

    public static PluginExecutionResult<T>? ValidateSnapshot<T>(IToolExecutionContext context, SnapshotPrecondition? expectedSnapshot)
    {
        var result = context.Resolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : PluginExecutionResult<T>.Conflict(
                new ToolError
                {
                    Code = "SnapshotMismatch",
                    Message = "The request snapshot does not match the current workspace snapshot.",
                },
                RequiredAction.ResolveTargetAgain);
    }

    public static PluginExecutionResult<TData> CreateBoundedCollectionResult<TData, TItem>(
        IQueryContext context,
        IReadOnlyList<TItem> orderedItems,
        int maxResults,
        Func<IReadOnlyList<TItem>, bool, TData> createData)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(orderedItems);
        ArgumentNullException.ThrowIfNull(createData);

        var limitedCount = Math.Min(maxResults, orderedItems.Count);

        for (var count = limitedCount; count >= 0; count--)
        {
            var items = count == orderedItems.Count ? orderedItems : orderedItems.Take(count).ToArray();
            var hasMore = count < orderedItems.Count;
            var data = createData(items, hasMore);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _serializerOptions);

            if (bytes.Length <= context.MaxResponseBytes)
            {
                return PluginExecutionResult<TData>.Success(data);
            }
        }

        return Rejected<TData>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest);
    }

    public static async ValueTask<ISymbol?> TryCreateContainingSymbolAsync(Document document, int position, IToolExecutionContext context, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return null;
        }

        return await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, context.CurrentSolution.Workspace, cancellationToken).ConfigureAwait(false);
    }

    public static SymbolSelector? CreateSourceSymbolSelector(ISymbol symbol, IWorkspaceResolver resolver)
    {
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return null;
        }

        var resolvedLocation = resolver.CreateResolvedLocation(sourceLocation);
        return CreateLocationSymbolSelector(resolvedLocation);
    }

    public static SymbolSelector? CreateLocationSymbolSelector(ResolvedLocation? resolvedLocation)
    {
        if (resolvedLocation?.Document is null || resolvedLocation.Span is null)
        {
            return null;
        }

        return new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = resolvedLocation.Document.Path,
                    },
                    Start = resolvedLocation.Span.Start,
                    Length = resolvedLocation.Span.Length,
                },
            },
        };
    }

    public static LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation)
    {
        if (resolvedLocation?.Document is null || resolvedLocation.Span is null)
        {
            return null;
        }

        return new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = resolvedLocation.Document.Path,
                },
                Start = resolvedLocation.Span.Start,
                Length = resolvedLocation.Span.Length,
            },
        };
    }

    internal sealed record ResolutionResult<TValue, TResponse>
        where TValue : class
    {
        public PluginExecutionResult<TResponse>? Rejection { get; init; }

        public TValue? Value { get; init; }

        [MemberNotNullWhen(true, nameof(Rejection))]
        [MemberNotNullWhen(false, nameof(Value))]
        public bool HasRejection => Rejection is not null;
    }
}
