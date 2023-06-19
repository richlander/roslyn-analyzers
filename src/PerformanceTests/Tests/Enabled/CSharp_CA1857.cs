﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.CSharp.Analyzers.Performance;
using PerformanceTests.Utilities;
using PerfUtilities;

namespace CSharpPerformanceTests.Enabled
{
    public class CSharp_CA1857
    {
        [IterationSetup]
        public static void CreateEnvironmentCA1856()
        {
            var sources = new List<(string name, string content)>();
            string attributeSource = @"#nullable enable
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class ConstantExpectedAttribute : Attribute
    {
        public object? Min { get; set; }
        public object? Max { get; set; }
    }
}";
            sources.Add(("attributeSource", attributeSource));

            string testClass = @"
using System;
using System.Diagnostics.CodeAnalysis;
#nullable enable

namespace ConstantExpectedTest
{
    public class Test
    {
        public void TestMethodInt32([ConstantExpected] int val) { }
        public void TestMethodInt32Ex([ConstantExpected(Min = 0, Max = 15)] int val) { }
    }
    public interface ITestInterface<T>
    {
        void TestMethod([ConstantExpected] T val) { }
    }
}
";
            sources.Add(("testClass", testClass));
            for (var i = 0; i < Constants.Number_Of_Code_Files; i++)
            {
                var name = "TypeName" + i;
                sources.Add((name, @$"
using System;
using ConstantExpectedTest;

class {name}
{{
    private readonly Test _test;
    
    public {name}(Test test)
    {{
        _test = test;
    }}

    public void Test(int a)
    {{
        _test.TestMethodInt32(20);
        _test.TestMethodInt32Ex(20); // diagnostic
    }}

    private sealed class TestImpl : ITestInterface<int>
    {{
        public void TestMethod(int val) {{ }}  // diagnostic
    }}
}}
"));
            }

            var compilation = CSharpCompilationHelper.CreateAsync(sources.ToArray()).GetAwaiter().GetResult();
            BaselineCompilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new EmptyAnalyzer()));
            CompilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CSharpConstantExpectedAnalyzer()));
        }

        private static CompilationWithAnalyzers BaselineCompilationWithAnalyzers;
        private static CompilationWithAnalyzers CompilationWithAnalyzers;

        [Benchmark]
        public async Task CA1857_DiagnosticsProduced()
        {
            var analysisResult = await CompilationWithAnalyzers.GetAnalysisResultAsync(CancellationToken.None);
            var diagnostics = analysisResult.GetAllDiagnostics(analysisResult.Analyzers.First());
            if (analysisResult.Analyzers.Length != 1)
            {
                throw new InvalidOperationException($"Expected a single analyzer but found '{analysisResult.Analyzers.Length}'");
            }

            if (analysisResult.CompilationDiagnostics.Count != 0)
            {
                throw new InvalidOperationException($"Expected no compilation diagnostics but found '{analysisResult.CompilationDiagnostics.Count}'");
            }

            if (diagnostics.Length != 2 * Constants.Number_Of_Code_Files)
            {
                throw new InvalidOperationException($"Expected '{2 * Constants.Number_Of_Code_Files:N0}' analyzer diagnostics but found '{diagnostics.Length}'");
            }
        }

        [Benchmark(Baseline = true)]
        public async Task CA1857_Baseline()
        {
            var analysisResult = await BaselineCompilationWithAnalyzers.GetAnalysisResultAsync(CancellationToken.None);
            var diagnostics = analysisResult.GetAllDiagnostics(analysisResult.Analyzers.First());
            if (analysisResult.Analyzers.Length != 1)
            {
                throw new InvalidOperationException($"Expected a single analyzer but found '{analysisResult.Analyzers.Length}'");
            }

            if (analysisResult.CompilationDiagnostics.Count != 0)
            {
                throw new InvalidOperationException($"Expected no compilation diagnostics but found '{analysisResult.CompilationDiagnostics.Count}'");
            }

            if (diagnostics.Length != 0)
            {
                throw new InvalidOperationException($"Expected no analyzer diagnostics but found '{diagnostics.Length}'");
            }
        }
    }
}
