using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aigamo.MatchGenerator.Analyzers;

// Reports AMG003 when a [GenerateMatch] base type is not marked with the C# 15 `closed`
// modifier. An open hierarchy can gain a derived type in another file or a partial part
// that the generated Match never handles, so the "exhaustive" match silently loses a case.
// `closed` constrains the hierarchy so the compiler (and this generator) can see every case.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MatchClosedBaseTypeAnalyzer : DiagnosticAnalyzer
{
	// C# 15 introduced `closed`. Below that the modifier does not exist, so warning would be
	// unactionable; compare against the numeric value (1500) because the enum member may be
	// absent from the Roslyn version this analyzer is compiled against.
	private const int FirstLanguageVersionWithClosed = 1500;

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
	[Diagnostics.MarkBaseTypeClosed];

	private static bool SupportsClosed(Compilation compilation)
	{
		// Take the effective version so `latest`/`default`/`preview` map to the concrete
		// version the running compiler supports rather than a sentinel: on a pre-C#-15
		// compiler `latest` resolves below the threshold and the rule stays silent.
		if (compilation.SyntaxTrees.FirstOrDefault()?.Options is not CSharpParseOptions options)
		{
			return false;
		}

		return (int)options.LanguageVersion.MapSpecifiedToEffectiveVersion()
			>= FirstLanguageVersionWithClosed;
	}

	private static bool IsClosed(INamedTypeSymbol type)
	{
		// A partial type is closed if any part carries the modifier. Match by text: the
		// `closed` keyword may not be a known SyntaxKind in the compiled-against Roslyn.
		foreach (var reference in type.DeclaringSyntaxReferences)
		{
			if (
				reference.GetSyntax() is TypeDeclarationSyntax declaration
				&& declaration.Modifiers.Any(m => m.ValueText == "closed")
			)
			{
				return true;
			}
		}

		return false;
	}

	private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol attribute)
	{
		var type = (INamedTypeSymbol)context.Symbol;

		// `closed` applies to a base class/record hierarchy. Enums are matched exhaustively
		// already; sealed and static classes cannot be a base, so a `closed` nudge is moot.
		if (type.TypeKind != TypeKind.Class || type.IsSealed || type.IsStatic)
		{
			return;
		}

		if (
			!type.GetAttributes()
				.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute))
		)
		{
			return;
		}

		if (IsClosed(type))
		{
			return;
		}

		context.ReportDiagnostic(
			Diagnostic.Create(
				Diagnostics.MarkBaseTypeClosed,
				type.Locations.FirstOrDefault(),
				type.Name
			)
		);
	}

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static start =>
		{
			// The attribute is emitted by this generator; if it is absent nothing here is
			// annotated. Resolving it once also gives an identity to compare against per type.
			var attribute = start.Compilation.GetTypeByMetadataName(
				"Aigamo.MatchGenerator.GenerateMatchAttribute"
			);
			if (attribute is null || !SupportsClosed(start.Compilation))
			{
				return;
			}

			start.RegisterSymbolAction(ctx => AnalyzeType(ctx, attribute), SymbolKind.NamedType);
		});
	}
}
