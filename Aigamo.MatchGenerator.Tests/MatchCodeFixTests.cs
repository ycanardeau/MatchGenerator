using System.Collections.Immutable;
using Aigamo.MatchGenerator.Analyzers;
using Aigamo.MatchGenerator.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aigamo.MatchGenerator.Tests;

public class MatchCodeFixTests
{
	// The generator emits GenerateMatchAttribute; supply it as an ordinary document so the
	// analyzer can resolve the symbol without running the generator inside the workspace.
	private const string Attribute = """
		using System;

		namespace Aigamo.MatchGenerator;

		[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
		internal sealed class GenerateMatchAttribute : Attribute;
		""";

	// Applies the AMG003 quick fix to the first flagged type in `source` and returns the
	// rewritten document text. AMG003 is version-gated on C# 15, so the project parses (and
	// analyzes) at Preview to open that gate.
	private static string ApplyClosedFix(string source)
	{
		using var workspace = new AdhocWorkspace();

		var projectId = ProjectId.CreateNewId();
		var solution = workspace.CurrentSolution
			.AddProject(projectId, "Tests", "Tests", LanguageNames.CSharp)
			.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
			.WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Func<>).Assembly.Location));

		solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "GenerateMatchAttribute.cs", Attribute);

		var documentId = DocumentId.CreateNewId(projectId);
		solution = solution.AddDocument(documentId, "Source.cs", source);
		var document = solution.GetDocument(documentId)!;

		var compilation = document.Project.GetCompilationAsync().GetAwaiter().GetResult()!;
		var diagnostics = compilation
			.WithAnalyzers([new MatchClosedBaseTypeAnalyzer()])
			.GetAnalyzerDiagnosticsAsync()
			.GetAwaiter()
			.GetResult();

		var diagnostic = diagnostics.Single(d => d.Id == "AMG003");

		var actions = ImmutableArray.CreateBuilder<CodeAction>();
		var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
		new MarkBaseTypeClosedCodeFixProvider().RegisterCodeFixesAsync(context).GetAwaiter().GetResult();

		var operations = actions.Single().GetOperationsAsync(CancellationToken.None).GetAwaiter().GetResult();
		var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

		return changedSolution.GetDocument(documentId)!.GetTextAsync().GetAwaiter().GetResult().ToString();
	}

	[Fact]
	public void Replaces_Abstract_With_Closed()
	{
		// A closed type is implicitly abstract, so `abstract closed` is a compile error (CS9384);
		// `abstract` must be replaced, not kept.
		var source = """
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

		var fixedSource = ApplyClosedFix(source);

		Assert.Contains("public closed record MaritalStatus", fixedSource);
		Assert.DoesNotContain("abstract", fixedSource);
	}

	[Fact]
	public void Inserts_Closed_After_Non_Abstract_Modifiers()
	{
		var source = """
			using Aigamo.MatchGenerator;

			namespace Test;

			[GenerateMatch]
			public record MaritalStatus
			{
				private MaritalStatus() { }

				public sealed record Single : MaritalStatus;
			}
			""";

		var fixedSource = ApplyClosedFix(source);

		Assert.Contains("public closed record MaritalStatus", fixedSource);
	}

	[Fact]
	public void Inserts_Closed_When_There_Are_No_Modifiers()
	{
		var source = """
			using Aigamo.MatchGenerator;

			namespace Test;

			[GenerateMatch]
			record Shape
			{
				private Shape() { }

				public sealed record Circle : Shape;
			}
			""";

		var fixedSource = ApplyClosedFix(source);

		// Layout (indentation, the [GenerateMatch] line) is preserved; only `closed` is added.
		Assert.Contains("\nclosed record Shape", fixedSource);
		Assert.Contains("[GenerateMatch]", fixedSource);
	}
}
