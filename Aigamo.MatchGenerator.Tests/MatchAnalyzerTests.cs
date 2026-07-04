using System.Collections.Immutable;
using Aigamo.MatchGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aigamo.MatchGenerator.Tests;

public class MatchAnalyzerTests
{
	// Runs the generator to materialize the Match extension, then runs the analyzer
	// over the resulting compilation (call sites bind against the generated method).
	private static ImmutableArray<Diagnostic> Analyze(string source)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source);

		var compilation = CSharpCompilation.Create(
			"Tests",
			[syntaxTree],
			[
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Func<>).Assembly.Location),
			],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		GeneratorDriver driver = CSharpGeneratorDriver.Create(new SourceGenerator());
		driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

		var diagnostics = outputCompilation
			.WithAnalyzers([new MatchNamedArgumentsAnalyzer()])
			.GetAnalyzerDiagnosticsAsync()
			.GetAwaiter()
			.GetResult();

		return [.. diagnostics.Where(d => d.Id == "AMG002")];
	}

	private const string Enum = """
		using Aigamo.MatchGenerator;

		namespace Test;

		[GenerateMatch]
		public enum Gender
		{
			Male = 1,
			Female,
		}
		""";

	private static string CallSite(string call) => $$"""
		{{Enum}}

		public static class Consumer
		{
			public static string Describe(Gender value) => value.{{call}};
		}
		""";

	[Fact]
	public void Reports_For_Each_Positional_Argument()
	{
		var source = CallSite("""Match(() => "m", () => "f")""");

		var diagnostics = Analyze(source);

		Assert.Equal(2, diagnostics.Length);
	}

	[Fact]
	public void Does_Not_Report_When_All_Arguments_Named()
	{
		var source = CallSite("""Match(onMale: () => "m", onFemale: () => "f")""");

		var diagnostics = Analyze(source);

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void Reports_Only_The_Positional_Argument_In_A_Mixed_Call()
	{
		var source = CallSite("""Match(onMale: () => "m", () => "f")""");

		var diagnostics = Analyze(source);

		Assert.Single(diagnostics);
	}

	[Fact]
	public void Does_Not_Report_For_Unrelated_Method_Named_Match()
	{
		// A Match method that is not an extension on a *MatchExtensions class must not
		// be flagged, even when called positionally.
		var source = """
			namespace Test;

			public static class NotAMatch
			{
				public static string Match(int value, string label) => label;

				public static string Use() => Match(1, "x");
			}
			""";

		var diagnostics = Analyze(source);

		Assert.Empty(diagnostics);
	}
}
