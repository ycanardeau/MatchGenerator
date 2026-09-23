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

	public static readonly DiagnosticDescriptor MarkBaseTypeClosed = new(
		id: "AMG003",
		title: "Mark the Match base type as closed",
		messageFormat: "Mark base type '{0}' as closed so its generated Match stays exhaustive as derived types change",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A [GenerateMatch] base type whose hierarchy is open can gain a derived type the generated Match does not handle, silently losing exhaustiveness. The C# 15 'closed' modifier constrains the hierarchy so every case is known at compile time."
	);

	public static readonly DiagnosticDescriptor InvalidParameterPrefix = new(
		id: "AMG004",
		title: "Invalid MatchGeneratorParameterPrefix",
		messageFormat: "MatchGeneratorParameterPrefix '{0}' is not a valid C# identifier; no prefix was applied",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "MatchGeneratorParameterPrefix is spliced directly into generated parameter names, so it must be a valid C# identifier (or empty) on its own. Falling back to no prefix silently would leave call sites written for the configured prefix failing with unrelated-looking errors, so this is an error by default rather than a warning that's easy to miss."
	);
}
