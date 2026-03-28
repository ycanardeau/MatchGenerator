using Microsoft.CodeAnalysis;

namespace Aigamo.MatchGenerator.Extensions;

internal static class EnumerableExtensions
{
	// https://github.com/dotnet/roslyn/blob/8f24ed69cbbf377573c403d6c8febbc92b560343/src/Compilers/Core/Portable/InternalUtilities/EnumerableExtensions.cs#L287
	public static IncrementalValuesProvider<T> WhereNotNull<T>(this IncrementalValuesProvider<T?> source)
		where T : class
	{
		return source.Where(x => x is not null)!;
	}
}
