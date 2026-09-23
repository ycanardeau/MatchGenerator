namespace Aigamo.MatchGenerator;

internal static class Constants
{
	public const string MatchExtensionClassSuffix = "MatchExtensions";

	// MSBuild property name for the parameter name prefix (empty by default, e.g. "on" for onRed).
	// Flows into AnalyzerConfigOptionsProvider.GlobalOptions as build_property.<name> via
	// CompilerVisibleProperty, declared in buildTransitive/Aigamo.MatchGenerator.props.
	public const string ParameterPrefixPropertyName = "MatchGeneratorParameterPrefix";
}
