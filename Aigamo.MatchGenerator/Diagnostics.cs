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
}
