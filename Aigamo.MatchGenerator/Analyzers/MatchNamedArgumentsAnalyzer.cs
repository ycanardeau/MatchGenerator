using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aigamo.MatchGenerator.Analyzers;

// Reports AMG002 when a generated Match extension method is called with positional
// arguments. Match parameters are emitted in declaration order, so a positional call
// silently rebinds to the wrong case when a case is added, removed, or reordered.
// Named arguments (onFoo:) are order-independent, hence the on-prefixed parameter names.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MatchNamedArgumentsAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[Diagnostics.UseNamedArgumentsForMatch];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
	}

	private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		// Only the idiomatic reduced form `value.Match(...)`. The unreduced static form
		// `FooMatchExtensions.Match(value, ...)` carries the receiver as a positional
		// argument that is legitimately unnamed, so mapping args to cases is ambiguous
		// there; skip it.
		if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
			is not IMethodSymbol
			{
				Name: "Match",
				MethodKind: MethodKind.ReducedExtension,
				ContainingType: { IsStatic: true } containingType,
			})
		{
			return;
		}

		if (!containingType.Name.EndsWith(Constants.MatchExtensionClassSuffix, StringComparison.Ordinal))
		{
			return;
		}

		// In reduced form the argument list holds exactly the case handlers (the
		// receiver is the member-access target, not an argument), so every unnamed
		// argument here is a case passed positionally.
		foreach (var argument in invocation.ArgumentList.Arguments)
		{
			if (argument.NameColon is null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					Diagnostics.UseNamedArgumentsForMatch,
					argument.GetLocation()));
			}
		}
	}
}
