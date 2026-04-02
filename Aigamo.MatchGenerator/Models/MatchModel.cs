using System.Diagnostics;

namespace Aigamo.MatchGenerator.Models;

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

internal static class MatchModelMatchExtensions
{
	public static U Match<U>(
		this MatchModel value,
		Func<EnumMatchModel, U> onEnumMatchModel,
		Func<UnionMatchModel, U> onUnionMatchModel
	)
	{
		return value switch
		{
			EnumMatchModel x => onEnumMatchModel(x),
			UnionMatchModel x => onUnionMatchModel(x),
			_ => throw new UnreachableException(),
		};
	}
}
