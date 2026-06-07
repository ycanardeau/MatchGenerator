namespace Aigamo.MatchGenerator.Models;

[GenerateMatch]
internal abstract record MatchModel(
	string Name,
	string? Namespace,
	string Accessibility,
	string HintName
);

internal sealed record EnumMatchModel(
	string Name,
	string? Namespace,
	string Accessibility,
	string[] Members
) : MatchModel(
	Name,
	Namespace,
	Accessibility,
	$"{Name}{Constants.MatchExtensionClassSuffix}.g.cs"
);

internal sealed record UnionMatchModel(
	string Name,
	string? Namespace,
	string Accessibility,
	string[] DerivedTypes
) : MatchModel(
	Name,
	Namespace,
	Accessibility,
	$"{Name}{Constants.MatchExtensionClassSuffix}.g.cs"
);
