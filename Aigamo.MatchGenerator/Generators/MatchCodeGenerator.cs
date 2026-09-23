using Aigamo.MatchGenerator.Models;

namespace Aigamo.MatchGenerator.Generators;

internal static class MatchCodeGenerator
{
	public static string Generate(MatchModel model, string parameterPrefix)
	{
		return model.Match(
			Enum: x => EnumCodeGenerator.Generate(x, parameterPrefix),
			Union: x => UnionCodeGenerator.Generate(x, parameterPrefix)
		);
	}
}
