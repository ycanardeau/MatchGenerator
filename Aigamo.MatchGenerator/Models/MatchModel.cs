namespace Aigamo.MatchGenerator.Models;

// Name is the derived type's simple name, used for the parameter suffix (onSingle).
// TypeName is qualified by containing types (MaritalStatus.Single for nested cases) so
// the generated extension class, which lives outside the base type, can reference it.
internal sealed record DerivedType(string Name, string TypeName);

// Name is the target's simple name, used for the generated class name and hint name.
// TypeName is qualified by containing types (Container.MaritalStatus for a nested target)
// so the generated extension class, which lives outside the target, can reference it.
[GenerateMatch]
internal closed record MatchModel
{
	public string HintName { get; }

	private MatchModel(string hintName)
	{
		HintName = hintName;
	}

	internal sealed record Enum(
		string Name,
		string TypeName,
		string? Namespace,
		string Accessibility,
		string[] Members
	) : MatchModel($"{Name}{Constants.MatchExtensionClassSuffix}.g.cs");

	internal sealed record Union(
		string Name,
		string TypeName,
		string? Namespace,
		string Accessibility,
		DerivedType[] DerivedTypes
	) : MatchModel($"{Name}{Constants.MatchExtensionClassSuffix}.g.cs");
}
