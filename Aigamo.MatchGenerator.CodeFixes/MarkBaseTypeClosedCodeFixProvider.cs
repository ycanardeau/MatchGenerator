using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aigamo.MatchGenerator.CodeFixes;

// Quick fix for AMG003 (see MatchClosedBaseTypeAnalyzer): adds the C# 15 `closed` modifier
// to a [GenerateMatch] base type so its generated Match stays exhaustive. `closed` is written
// as an identifier token (its ValueText is still "closed", which is exactly what the analyzer
// matches on) so this compiles against a Roslyn that predates the keyword; the emitted source
// text is identical to a real `closed` keyword either way.
//
// This lives in its own assembly because a code fix references Microsoft.CodeAnalysis.Workspaces,
// which is absent during command-line compilation. Keeping it out of the analyzer/generator
// assembly is what RS1038 asks for; both DLLs are packed side by side under analyzers/dotnet/cs.
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MarkBaseTypeClosedCodeFixProvider))]
[Shared]
public sealed class MarkBaseTypeClosedCodeFixProvider : CodeFixProvider
{
	// Kept in sync with Diagnostics.MarkBaseTypeClosed.Id in the analyzer assembly; the id is a
	// stable public contract (README, AnalyzerReleases), so it is duplicated rather than shared
	// to avoid a hard reference from this assembly to the generator assembly.
	private const string DiagnosticId = "AMG003";

	private const string Title = "Mark base type as closed";

	public override ImmutableArray<string> FixableDiagnosticIds { get; } = [DiagnosticId];

	// Each fix adds one modifier to one declaration in isolation, so batching is safe.
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
		{
			return;
		}

		var diagnostic = context.Diagnostics[0];

		// AMG003 is reported at the type's name identifier; walk up to the declaration.
		if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>() is not
			{ } declaration)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Title,
				_ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, WithClosed(declaration)))),
				equivalenceKey: DiagnosticId
			),
			diagnostic
		);
	}

	private static TypeDeclarationSyntax WithClosed(TypeDeclarationSyntax declaration)
	{
		// A closed type is always implicitly abstract, so `abstract closed` is a compile error
		// (CS9384). Replace an existing `abstract` in place rather than adding alongside it:
		// `public abstract record` -> `public closed record`, keeping accessibility and layout.
		var @abstract = declaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.AbstractKeyword));
		if (@abstract.IsKind(SyntaxKind.AbstractKeyword))
		{
			var replacement = SyntaxFactory.Identifier(@abstract.LeadingTrivia, "closed", @abstract.TrailingTrivia);
			return declaration.WithModifiers(declaration.Modifiers.Replace(@abstract, replacement));
		}

		if (declaration.Modifiers.Count > 0)
		{
			// Other modifiers only (`public record` -> `public closed record`): place `closed`
			// after the last modifier, taking over its trailing trivia (the space before the
			// type keyword) and leaving a single space of its own in front.
			var last = declaration.Modifiers[declaration.Modifiers.Count - 1];
			var closed = SyntaxFactory.Identifier(
				SyntaxFactory.TriviaList(SyntaxFactory.Space),
				"closed",
				last.TrailingTrivia
			);

			return declaration.WithModifiers(
				declaration.Modifiers.Replace(last, last.WithTrailingTrivia()).Add(closed)
			);
		}

		// No modifiers (`record Foo` -> `closed record Foo`): move the type keyword's leading
		// trivia (indentation, newlines, doc comments) onto `closed` so the layout is kept.
		var keyword = declaration.Keyword;
		var closedFirst = SyntaxFactory.Identifier(
			keyword.LeadingTrivia,
			"closed",
			SyntaxFactory.TriviaList(SyntaxFactory.Space)
		);

		return declaration
			.WithModifiers(SyntaxFactory.TokenList(closedFirst))
			.WithKeyword(keyword.WithLeadingTrivia());
	}
}
