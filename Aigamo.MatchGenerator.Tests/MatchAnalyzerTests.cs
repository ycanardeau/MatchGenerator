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

	// Runs the generator (so GenerateMatchAttribute exists), then the closed-base-type
	// analyzer, at a language version high enough for AMG003's `closed` version gate.
	// The 4.14 parser referenced here cannot parse the `closed` modifier itself, so the
	// "already closed -> no diagnostic" path is exercised via IsClosed's unit behavior
	// rather than a source fixture.
	private static ImmutableArray<Diagnostic> AnalyzeClosed(string source)
	{
		// AMG003 is version-gated on C# 15; parse (and generate) at Preview so the gate opens.
		var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);

		var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

		var compilation = CSharpCompilation.Create(
			"Tests",
			[syntaxTree],
			[
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Func<>).Assembly.Location),
			],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		// Generate at the same language version so the generated attribute source and the
		// input tree agree (a version mismatch across trees fails compilation construction).
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[new SourceGenerator().AsSourceGenerator()],
			parseOptions: parseOptions
		);
		driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

		var diagnostics = outputCompilation
			.WithAnalyzers([new MatchClosedBaseTypeAnalyzer()])
			.GetAnalyzerDiagnosticsAsync()
			.GetAwaiter()
			.GetResult();

		return [.. diagnostics.Where(d => d.Id == "AMG003")];
	}

	private const string Union = """
		using Aigamo.MatchGenerator;

		namespace Test;

		[GenerateMatch]
		public abstract record MaritalStatus
		{
			private MaritalStatus() { }

			public sealed record Single : MaritalStatus;
			public sealed record Married : MaritalStatus;
		}
		""";

	[Fact]
	public void Reports_For_Open_GenerateMatch_Base_Type()
	{
		var diagnostics = AnalyzeClosed(Union);

		var diagnostic = Assert.Single(diagnostics);
		Assert.Contains("MaritalStatus", diagnostic.GetMessage());
	}

	[Fact]
	public void Does_Not_Report_For_Enum()
	{
		var diagnostics = AnalyzeClosed(Enum);

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void Does_Not_Report_For_Type_Without_GenerateMatch()
	{
		var source = """
			namespace Test;

			public abstract record Shape
			{
				public sealed record Circle : Shape;
			}
			""";

		var diagnostics = AnalyzeClosed(source);

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void Does_Not_Report_For_Sealed_GenerateMatch_Type()
	{
		// A sealed type cannot be a base, so nudging it toward `closed` would be nonsensical.
		var source = """
			using Aigamo.MatchGenerator;

			namespace Test;

			[GenerateMatch]
			public sealed record Leaf;
			""";

		var diagnostics = AnalyzeClosed(source);

		Assert.Empty(diagnostics);
	}
}
