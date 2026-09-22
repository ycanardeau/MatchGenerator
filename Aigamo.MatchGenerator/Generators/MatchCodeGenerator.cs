using Aigamo.MatchGenerator.Models;

namespace Aigamo.MatchGenerator.Generators;

internal static class MatchCodeGenerator
{
	public static string Generate(MatchModel model, string parameterPrefix)
	{
		return model.Match(
			onEnum: x => EnumCodeGenerator.Generate(x, parameterPrefix),
			onUnion: x => UnionCodeGenerator.Generate(x, parameterPrefix)
		);
	}
}
