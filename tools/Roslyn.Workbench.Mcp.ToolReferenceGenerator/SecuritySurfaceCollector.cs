using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.Configuration;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.ErrorReporting.Availability;
using Roslyn.Workbench.Mcp.ErrorReporting.Configuration;
using Roslyn.Workbench.Mcp.ErrorReporting.Projection;
using Roslyn.Workbench.Mcp.PluginLoading;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Execution;
using Roslyn.Workbench.Mcp.Protocol.Results;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Recovery;
using Roslyn.Workbench.Mcp.Workspace.Results;
using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Derives the canonical security-relevant surface from typed contracts and production Host composition.
/// </summary>
internal static class SecuritySurfaceCollector
{
    /// <summary>
    /// Collects every registered surface category.
    /// </summary>
    /// <param name="stateDirectory">The isolated state directory used for Host composition.</param>
    /// <param name="cancellationToken">The token used to cancel collection.</param>
    /// <returns>Unique surface items ordered by stable key.</returns>
    public static async Task<IReadOnlyList<SecuritySurfaceItem>> CollectAsync(
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        var items = new List<SecuritySurfaceItem>();
        AddOperationalModes(items);
        AddEnum(items, "policy:source-mutation", Enum.GetValues<SourceMutationPolicy>());
        AddEnum(items, "policy:commit-authorisation", Enum.GetValues<CommitAuthorisationPolicy>());
        AddEnum(items, "policy:commit-validation", Enum.GetValues<CommitValidationPolicy>());
        AddEnum(items, "continuation", Enum.GetValues<ToolContinuationKind>());
        AddRequiredActionMappings(items);
        AddEnum(items, "recovery-state", Enum.GetValues<RecoveryState>());
        AddEnum(items, "recovery-operation", Enum.GetValues<WorkspaceFileOperation>());
        AddEnum(items, "policy:external-document", Enum.GetValues<ExternalDocumentPolicy>());
        AddEnum(items, "policy:generated-source", Enum.GetValues<GeneratedSourcePolicy>());
        AddEnum(items, "policy:error-report-consent", Enum.GetValues<ErrorReportingConsentMode>());
        AddStatusFields(items);
        AddContractShapes(items);
        AddConstants(items);
        AddPluginBoundary(items);
        await AddToolAvailabilityAsync(items, stateDirectory, cancellationToken);

        var duplicate = items.GroupBy(static item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Security surface key '{duplicate.Key}' is duplicated.");
        }

        return items.OrderBy(static item => item.Key, StringComparer.Ordinal).ToArray();
    }

    private static void AddOperationalModes(List<SecuritySurfaceItem> items)
    {
        foreach (var mode in OperationalModeNames.All)
        {
            var supportedValidations = Enum.GetValues<CommitValidationPolicy>()
                .Where(validation => IsConfigurationSupported(mode, validation))
                .ToArray();

            items.Add(new SecuritySurfaceItem
            {
                Key = $"operational-mode:{OperationalModeNames.GetName(mode)}",
                Category = "operational-mode",
                Facts = new JsonObject
                {
                    ["supportedCommitValidation"] = new JsonArray(supportedValidations
                        .Select(static validation => JsonValue.Create(SecuritySurfaceNames.GetEnumName(validation)))
                        .ToArray()),
                },
            });

            foreach (var validation in supportedValidations)
            {
                var policy = OperationalPolicyResolver.Resolve(mode, validation);
                items.Add(new SecuritySurfaceItem
                {
                    Key = $"effective-policy:{OperationalModeNames.GetName(mode)}:{SecuritySurfaceNames.GetEnumName(validation)}",
                    Category = "effective-policy",
                    Facts = new JsonObject
                    {
                        ["sourceMutation"] = SecuritySurfaceNames.GetEnumName(policy.SourceMutation),
                        ["commitAuthorisation"] = SecuritySurfaceNames.GetEnumName(policy.CommitAuthorisation),
                        ["commitValidation"] = SecuritySurfaceNames.GetEnumName(policy.CommitValidation),
                    },
                });
            }
        }
    }

    private static void AddRequiredActionMappings(List<SecuritySurfaceItem> items)
    {
        foreach (var action in Enum.GetValues<RequiredAction>())
        {
            var continuation = RequiredActionContinuationMapper.Map(action)
                ?? throw new InvalidOperationException($"Required action '{action}' does not publish a continuation.");

            items.Add(new SecuritySurfaceItem
            {
                Key = $"required-action:{SecuritySurfaceNames.GetEnumName(action)}",
                Category = "required-action",
                Facts = new JsonObject
                {
                    ["numericValue"] = (int)action,
                    ["continuationKind"] = SecuritySurfaceNames.GetEnumName(continuation.Kind),
                    ["tool"] = continuation.Tool,
                    ["tools"] = CreateToolsNode(continuation.Tools),
                },
            });
        }
    }

    private static void AddEnum<TEnum>(
        List<SecuritySurfaceItem> items,
        string keyPrefix,
        IReadOnlyList<TEnum> values)
        where TEnum : struct, Enum
    {
        foreach (var value in values)
        {
            items.Add(new SecuritySurfaceItem
            {
                Key = $"{keyPrefix}:{SecuritySurfaceNames.GetEnumName(value)}",
                Category = keyPrefix,
                Facts = new JsonObject
                {
                    ["numericValue"] = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture),
                },
            });
        }
    }

    private static void AddStatusFields(List<SecuritySurfaceItem> items)
    {
        var nullability = new NullabilityInfoContext();
        var properties = typeof(ServerConfiguration).GetProperties()
            .OrderBy(static property => property.Name, StringComparer.Ordinal);

        foreach (var property in properties)
        {
            var nullabilityInfo = nullability.Create(property);
            var required = IsRequiredStatusField(property);

            items.Add(new SecuritySurfaceItem
            {
                Key = $"status-field:{ToCamelCase(property.Name)}",
                Category = "status-field",
                Facts = new JsonObject
                {
                    ["type"] = GetSchemaType(property.PropertyType),
                    ["required"] = required,
                    ["nullable"] = nullabilityInfo.WriteState == NullabilityState.Nullable,
                },
            });
        }
    }

    private static JsonArray? CreateToolsNode(IReadOnlyList<string>? tools)
    {
        if (tools is null)
        {
            return null;
        }

        return new JsonArray(tools.Select(static tool => JsonValue.Create(tool)).ToArray());
    }

    private static void AddContractShapes(List<SecuritySurfaceItem> items)
    {
        var pending = new Queue<Type>(
        [
            typeof(ServerStatusData),
            typeof(ToolContinuation),
            typeof(ToolError),
            typeof(WorkspaceStatusData),
        ]);

        var collected = new HashSet<Type>();
        var nullability = new NullabilityInfoContext();
        while (pending.TryDequeue(out var contractType))
        {
            if (!collected.Add(contractType))
            {
                continue;
            }

            if (contractType.IsEnum)
            {
                AddRuntimeEnum(items, $"contract-enum:{GetContractKey(contractType)}", contractType);
                continue;
            }

            var properties = new JsonObject();
            foreach (var property in contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                var propertyType = UnwrapContractType(property.PropertyType);
                if (IsProductContractType(propertyType))
                {
                    pending.Enqueue(propertyType);
                }

                var nullabilityInfo = nullability.Create(property);
                var jsonIgnore = property.GetCustomAttribute<JsonIgnoreAttribute>();
                properties[ToCamelCase(property.Name)] = new JsonObject
                {
                    ["type"] = GetContractTypeName(property.PropertyType),
                    ["required"] = IsRequiredStatusField(property),
                    ["nullable"] = nullabilityInfo.WriteState == NullabilityState.Nullable,
                    ["omittedWhenNull"] = jsonIgnore?.Condition == JsonIgnoreCondition.WhenWritingNull,
                };
            }

            items.Add(new SecuritySurfaceItem
            {
                Key = $"contract-shape:{GetContractKey(contractType)}",
                Category = "contract-shape",
                Facts = new JsonObject
                {
                    ["properties"] = properties,
                },
            });
        }
    }

    private static void AddConstants(List<SecuritySurfaceItem> items)
    {
        var errorCodes = PublishedErrorCodeCatalogue.ConstantOwnerTypes
            .SelectMany(GetStringConstants)
            .Concat(PublishedErrorCodeCatalogue.EnumOwnerTypes.SelectMany(Enum.GetNames))
            .Distinct(StringComparer.Ordinal);

        AddValues(items, "error", errorCodes);
        AddStringConstants(items, "recovery-status", typeof(RecoveryStatusCodes));
        AddConstant(items, "warning:generated-source-mutation", "warning", GeneratedSourceMutationWarnings.WarningCode);
        AddConstant(items, $"recovery-format:{RecoveryFormatVersions.V1}", "recovery-format", RecoveryFormatVersions.V1);
        AddConstant(items, $"receipt-digest:{TransactionReviewIdentityService.Algorithm}", "receipt-digest", TransactionReviewIdentityService.Algorithm);
        AddConstant(items, $"compiler-error-identity:{TransactionCompilerValidationOutcome.CurrentAlgorithm}", "compiler-error-identity", TransactionCompilerValidationOutcome.CurrentAlgorithm);
        AddConstant(items, $"error-report-schema:{ExternalErrorReport.CurrentSchemaVersion}", "error-report-schema", ExternalErrorReport.CurrentSchemaVersion);
        AddConstant(items, $"error-report-format:{ExternalErrorReport.CurrentReportFormatVersion}", "error-report-format", ExternalErrorReport.CurrentReportFormatVersion);
        AddConstant(items, $"plugin-api:{PluginApiVersions.V1}", "plugin-api", PluginApiVersions.V1);
    }

    private static void AddStringConstants(List<SecuritySurfaceItem> items, string category, Type type)
    {
        foreach (var value in GetStringConstants(type))
        {
            AddConstant(items, $"{category}:{value}", category, value);
        }
    }

    private static IEnumerable<string> GetStringConstants(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .OrderBy(static field => field.Name, StringComparer.Ordinal)
            .Select(field => (string?)field.GetRawConstantValue()
                ?? throw new InvalidOperationException($"Security surface constant '{type.FullName}.{field.Name}' is null."));
    }

    private static void AddConstant(List<SecuritySurfaceItem> items, string key, string category, object value)
    {
        items.Add(new SecuritySurfaceItem
        {
            Key = key,
            Category = category,
            Facts = new JsonObject
            {
                ["value"] = JsonValue.Create(value),
            },
        });
    }

    private static void AddPluginBoundary(List<SecuritySurfaceItem> items)
    {
        var defaults = new StartupOptions();
        AddConstant(items, "plugin-loading:default", "plugin-loading", defaults.ExternalPluginsEnabled);

        foreach (var mode in OperationalModeNames.All)
        {
            var policy = OperationalPolicyResolver.Resolve(mode);
            items.Add(new SecuritySurfaceItem
            {
                Key = $"plugin-publication:{OperationalModeNames.GetName(mode)}",
                Category = "plugin-publication",
                Facts = new JsonObject
                {
                    ["queryPublished"] = PluginToolPublicationPolicy.ShouldPublish(ToolKind.Query, policy.SourceMutationEnabled),
                    ["mutationPublished"] = PluginToolPublicationPolicy.ShouldPublish(ToolKind.Mutation, policy.SourceMutationEnabled),
                },
            });
        }

        items.Add(new SecuritySurfaceItem
        {
            Key = "plugin-loading:execution-boundary",
            Category = "plugin-loading",
            Facts = new JsonObject
            {
                ["processIsolation"] = false,
                ["hostAuthority"] = true,
            },
        });
    }

    private static async Task AddToolAvailabilityAsync(
        List<SecuritySurfaceItem> items,
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        var availability = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var successEnvelopes = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        JsonObject? failureEnvelope = null;
        foreach (var mode in OperationalModeNames.All)
        {
            foreach (var validation in Enum.GetValues<CommitValidationPolicy>())
            {
                if (!IsConfigurationSupported(mode, validation))
                {
                    continue;
                }

                foreach (var reportingEnabled in new[] { false, true })
                {
                    var configuration = string.Join(
                        ';',
                        OperationalModeNames.GetName(mode),
                        SecuritySurfaceNames.GetEnumName(validation),
                        reportingEnabled ? "reporting-enabled" : "reporting-disabled");

                    var tools = await ProductionToolCatalogueComposer.ComposeAsync(
                        stateDirectory,
                        mode,
                        validation,
                        reportingEnabled,
                        cancellationToken);

                    foreach (var tool in tools)
                    {
                        if (!availability.TryGetValue(tool.Name, out var configurations))
                        {
                            configurations = [];
                            availability.Add(tool.Name, configurations);
                        }

                        configurations.Add(configuration);
                    }

                    failureEnvelope = ValidateAndCollectOutputEnvelopes(
                        tools,
                        successEnvelopes,
                        failureEnvelope);
                }
            }
        }

        items.Add(new SecuritySurfaceItem
        {
            Key = "failure-envelope:standard",
            Category = "failure-envelope",
            Facts = new JsonObject
            {
                ["schema"] = failureEnvelope
                    ?? throw new InvalidOperationException("Production Host composition did not publish a failure envelope."),
                ["runtimeEnvelopes"] = CreateRuntimeFailureEnvelopes(),
            },
        });

        items.Add(new SecuritySurfaceItem
        {
            Key = "success-envelope:all-published-tools",
            Category = "success-envelope",
            Facts = new JsonObject
            {
                ["tools"] = new JsonObject(successEnvelopes
                    .OrderBy(static item => item.Key, StringComparer.Ordinal)
                    .Select(static item => KeyValuePair.Create<string, JsonNode?>(item.Key, item.Value))
                    .ToArray()),
            },
        });

        items.Add(new SecuritySurfaceItem
        {
            Key = "configuration:inspection-only:no-new-compiler-errors",
            Category = "configuration",
            Facts = new JsonObject
            {
                ["supported"] = false,
            },
        });

        foreach (var tool in availability.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            items.Add(new SecuritySurfaceItem
            {
                Key = $"tool:{tool.Key}",
                Category = "tool",
                Facts = new JsonObject
                {
                    ["availability"] = new JsonArray(tool.Value
                        .Order(StringComparer.Ordinal)
                        .Select(static value => JsonValue.Create(value))
                        .ToArray()),
                },
            });
        }
    }

    private static void AddValues(
        List<SecuritySurfaceItem> items,
        string category,
        IEnumerable<string> values)
    {
        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            AddConstant(items, $"{category}:{value}", category, value);
        }
    }

    private static JsonObject ValidateAndCollectOutputEnvelopes(
        IReadOnlyList<Tool> tools,
        Dictionary<string, JsonObject> successEnvelopes,
        JsonObject? expectedEnvelope)
    {
        foreach (var tool in tools)
        {
            if (tool.OutputSchema is not { } outputSchema)
            {
                throw new InvalidOperationException($"Tool '{tool.Name}' does not publish its full output schema.");
            }

            var successEnvelope = CreateSuccessEnvelopeNode(outputSchema);
            if (successEnvelopes.TryGetValue(tool.Name, out var expectedSuccessEnvelope))
            {
                if (!JsonNode.DeepEquals(expectedSuccessEnvelope, successEnvelope))
                {
                    throw new InvalidOperationException($"Tool '{tool.Name}' publishes different success envelopes across supported configurations.");
                }
            }
            else
            {
                successEnvelopes.Add(tool.Name, successEnvelope);
            }

            var envelope = CreateFailureEnvelopeNode(outputSchema);
            if (expectedEnvelope is null)
            {
                expectedEnvelope = envelope;
                continue;
            }

            if (!JsonNode.DeepEquals(expectedEnvelope, envelope))
            {
                throw new InvalidOperationException($"Tool '{tool.Name}' does not publish the standard failure envelope.");
            }
        }

        return expectedEnvelope
            ?? throw new InvalidOperationException("Production Host composition did not publish any tools.");
    }

    private static JsonObject CreateSuccessEnvelopeNode(JsonElement outputSchema)
    {
        var alternatives = outputSchema.GetProperty("oneOf");
        if (alternatives.GetArrayLength() < 2)
        {
            throw new InvalidOperationException("A production tool output schema does not publish success and failure alternatives.");
        }

        var successSchema = alternatives[0];
        var envelope = JsonNode.Parse(successSchema.GetRawText())
            ?? throw new InvalidOperationException("A production success envelope could not be parsed.");

        var referencedDefinitions = new JsonObject();
        AddReferencedDefinitions(envelope, outputSchema.GetProperty("$defs"), referencedDefinitions);

        return new JsonObject
        {
            ["variant"] = envelope,
            ["definitions"] = referencedDefinitions,
        };
    }

    private static JsonObject CreateFailureEnvelopeNode(JsonElement outputSchema)
    {
        var alternatives = outputSchema.GetProperty("oneOf");
        if (alternatives.GetArrayLength() < 2)
        {
            throw new InvalidOperationException("A production tool output schema does not publish success and failure alternatives.");
        }

        var failureAlternatives = new JsonArray();
        var referencedDefinitions = new JsonObject();
        foreach (var failureSchema in alternatives.EnumerateArray().Skip(1))
        {
            var envelope = JsonNode.Parse(failureSchema.GetRawText())
                ?? throw new InvalidOperationException("A production failure envelope could not be parsed.");

            failureAlternatives.Add(envelope);
            AddReferencedDefinitions(envelope, outputSchema.GetProperty("$defs"), referencedDefinitions);
        }

        return new JsonObject
        {
            ["variants"] = failureAlternatives,
            ["definitions"] = referencedDefinitions,
        };
    }

    private static JsonObject CreateRuntimeFailureEnvelopes()
    {
        var handledError = new ToolError
        {
            Code = HostToolErrorCodes.InvalidRequest,
            Message = "Failure message",
            CorrelationId = "00000000-0000-0000-0000-000000000001",
        };

        var handledDiagnostics = new DiagnosticInfo[]
        {
            new()
            {
                Id = "DiagnosticId",
                Severity = DiagnosticSeverity.Error,
                Message = "Diagnostic message",
            },
        };

        var handledWarnings = new WarningInfo[]
        {
            new()
            {
                Code = "WarningCode",
                Message = "Warning message",
            },
        };

        var handled = ToolResultEnvelopeSerializer.CreateFailure(
            handledError,
            RequiredAction.Retry,
            handledDiagnostics,
            handledWarnings);

        var reporting = new ErrorReportingAvailability
        {
            State = ErrorReportingState.Available,
            CanPrepare = true,
            PrepareTool = ServerOwnedToolRegistration.PrepareErrorReportName,
        };

        var unhandled = ToolResultEnvelopeSerializer.CreateUnhandledException(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            reporting);

        return new JsonObject
        {
            ["handled"] = JsonNode.Parse(handled.GetRawText()),
            ["unhandled"] = JsonNode.Parse(unhandled.GetRawText()),
        };
    }

    private static void AddReferencedDefinitions(
        JsonNode node,
        JsonElement availableDefinitions,
        JsonObject referencedDefinitions)
    {
        if (node is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            var definitionName = text["#/$defs/".Length..];
            if (referencedDefinitions.ContainsKey(definitionName))
            {
                return;
            }

            var definition = JsonNode.Parse(availableDefinitions.GetProperty(definitionName).GetRawText())
                ?? throw new InvalidOperationException($"Failure-envelope definition '{definitionName}' could not be parsed.");

            referencedDefinitions[definitionName] = definition;
            AddReferencedDefinitions(definition, availableDefinitions, referencedDefinitions);
            return;
        }

        if (node is JsonObject objectNode)
        {
            foreach (var child in objectNode.Select(static property => property.Value).OfType<JsonNode>())
            {
                AddReferencedDefinitions(child, availableDefinitions, referencedDefinitions);
            }

            return;
        }

        if (node is JsonArray arrayNode)
        {
            foreach (var child in arrayNode.OfType<JsonNode>())
            {
                AddReferencedDefinitions(child, availableDefinitions, referencedDefinitions);
            }
        }
    }

    private static string GetSchemaType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string) || type == typeof(TimeSpan) || type.IsEnum)
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(int) || type == typeof(long))
        {
            return "integer";
        }

        return "object";
    }

    private static bool IsConfigurationSupported(
        OperationalMode mode,
        CommitValidationPolicy validation)
    {
        var options = new StartupOptions
        {
            OperationalMode = mode,
            CommitValidation = validation,
        };

        var validator = new StartupOptionsValidator();
        return validator.Validate(null, options).Succeeded;
    }

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Security surface keys are public lower-case kebab identifiers and must remain stable across generated baselines.")]
    private static string GetContractKey(Type type)
    {
        return (type.FullName ?? type.Name)
            .Replace('.', '-')
            .ToLowerInvariant();
    }

    private static void AddRuntimeEnum(
        List<SecuritySurfaceItem> items,
        string keyPrefix,
        Type type)
    {
        foreach (var value in Enum.GetValues(type).Cast<Enum>())
        {
            items.Add(new SecuritySurfaceItem
            {
                Key = $"{keyPrefix}:{SecuritySurfaceNames.GetEnumName(value)}",
                Category = keyPrefix,
                Facts = new JsonObject
                {
                    ["numericValue"] = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture),
                },
            });
        }
    }

    private static Type UnwrapContractType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsArray)
        {
            return GetArrayElementType(type);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    private static bool IsProductContractType(Type type)
    {
        return type.Namespace?.StartsWith("Roslyn.Workbench.Mcp", StringComparison.Ordinal) == true;
    }

    private static string GetContractTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return $"nullable<{GetContractTypeName(underlying)}>";
        }

        if (type.IsArray)
        {
            return $"array<{GetContractTypeName(GetArrayElementType(type))}>";
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return $"array<{GetContractTypeName(type.GetGenericArguments()[0])}>";
        }

        return type.FullName ?? type.Name;
    }

    private static Type GetArrayElementType(Type type)
    {
        return type.GetElementType()
            ?? throw new InvalidOperationException($"Array type '{type}' does not identify its element type.");
    }

    private static bool IsRequiredStatusField(PropertyInfo property)
    {
        if (property.GetCustomAttribute<RequiredMemberAttribute>() is not null)
        {
            return true;
        }

        return property.PropertyType.IsValueType
            && Nullable.GetUnderlyingType(property.PropertyType) is null;
    }

    private static string ToCamelCase(string value)
    {
        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
