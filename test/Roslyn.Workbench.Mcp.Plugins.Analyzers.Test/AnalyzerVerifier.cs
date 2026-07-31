using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

internal static class AnalyzerVerifier
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
    {
        var diagnostic = new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
        return diagnostic;
    }

    public static async Task VerifyAsync(
        string source,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest<PluginAuthoringAnalyzer>(source);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    public static async Task VerifyHandlerAsync(
        string source,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest<PluginHandlerAnalyzer>(source);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    public static async Task VerifyInvocationAsync(
        string source,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest<PluginInvocationAnalyzer>(source);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    public static async Task VerifyEntryPointAsync(
        string source,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest<PluginEntryPointAnalyzer>(source);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    public static async Task VerifyQueryCacheAsync(
        string source,
        params DiagnosticResult[] expected)
    {
        var test = CreateTest<PluginQueryCacheAnalyzer>(source);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    private static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> CreateTest<TAnalyzer>(
        string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = AnalyzerSourcePrelude.Code + source,
        };

        return test;
    }
}
