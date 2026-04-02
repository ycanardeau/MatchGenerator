using Aigamo.MatchGenerator.Models;

namespace Aigamo.MatchGenerator.Generators;

internal static class MatchCodeGenerator
{
	public static string Generate(MatchModel model)
	{
		return model.Match(
			onEnumMatchModel: x => EnumCodeGenerator.Generate(x),
			onUnionMatchModel: x => UnionCodeGenerator.Generate(x)
		);
	}
}
