using Aigamo.MatchGenerator.Models;

namespace Aigamo.MatchGenerator.Generators;

internal static class MatchCodeGenerator
{
	public static string Generate(MatchModel model)
	{
		return model.Match(
			onEnum: x => EnumCodeGenerator.Generate(x),
			onUnion: x => UnionCodeGenerator.Generate(x)
		);
	}
}
