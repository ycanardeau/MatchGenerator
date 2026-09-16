namespace Aigamo.MatchGenerator.Extensions;

internal static class TypeParameterExtensions
{
	/// <summary>
	/// Renders the base type's type parameters as a prefix for the generated <c>Match</c>
	/// method's type parameter list, which always ends in the result type <c>U</c>. A generic
	/// base like <c>Option&lt;T&gt;</c> yields <c>"T, "</c> so the method becomes
	/// <c>Match&lt;T, U&gt;</c>; a non-generic base yields the empty string, leaving
	/// <c>Match&lt;U&gt;</c>.
	/// </summary>
	public static string ToMethodTypeParameterPrefix(this string[] typeParameters)
	{
		return typeParameters.Length == 0 ? "" : string.Join(", ", typeParameters) + ", ";
	}
}
