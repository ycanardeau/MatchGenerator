using Microsoft.CodeAnalysis;

namespace Aigamo.MatchGenerator;

internal static class Diagnostics
{
	public static readonly DiagnosticDescriptor UnsupportedGenerateMatchForTarget = new(
		id: "AMG001",
		title: "GenerateMatchFor target has no cases",
		messageFormat: "Cannot generate Match for '{0}': GenerateMatchFor requires an enum or a base type with derived types declared in this compilation",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UseNamedArgumentsForMatch = new(
		id: "AMG002",
		title: "Use named arguments when calling Match",
		messageFormat: "Pass arguments to 'Match' by name so the call stays correct when cases are added or reordered",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Match parameters follow declaration order, so a positional argument can silently rebind to a different case when one is added, removed, or reordered. Named arguments are order-independent and the safe call style."
	);
}
