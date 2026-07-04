using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aigamo.MatchGenerator.Models;

internal static class MatchModelFactory
{
	private static Accessibility Min(Accessibility left, Accessibility right)
	{
		return (Accessibility)Math.Min((int)left, (int)right);
	}

	private static string ToCode(Accessibility accessibility)
	{
		return accessibility.Match(
			onInternal: () => "internal",
			onNotApplicable: () => "internal",
			onPrivate: () => "private",
			onProtected: () => "protected",
			onProtectedAndInternal: () => "private protected",
			onProtectedOrInternal: () => "protected internal",
			onPublic: () => "public"
		);
	}

	public static string GetEffectiveAccessibility(INamedTypeSymbol symbol)
	{
		var accessibility = symbol.DeclaredAccessibility;

		var current = symbol.ContainingType;
		while (current is not null)
		{
			accessibility = Min(accessibility, current.DeclaredAccessibility);
			current = current.ContainingType;
		}

		return ToCode(accessibility);
	}

	private static EnumMatchModel CreateEnumModel(INamedTypeSymbol type, string accessibility)
	{
		var members = type.GetMembers()
			.OfType<IFieldSymbol>()
			.Where(x => x.ConstantValue is not null)
			// Collapse aliased members that share the same constant value (e.g.
			// Accessibility.Friend == Accessibility.Internal). Emitting both would
			// produce duplicate switch case labels, which won't compile. GetMembers
			// preserves declaration order, so First() keeps the canonical member.
			.GroupBy(x => x.ConstantValue)
			.Select(g => g.First())
			// Emit parameters in declaration order rather than sorting by name. This
			// keeps the Match(...) parameter list append-stable: a member added at the
			// end of the enum becomes the last parameter, so existing positional call
			// sites keep compiling. (Reordering or inserting members mid-list still
			// shifts positions; named arguments are the only fully safe call style.)
			.ToList();

		var enumName = type.Name;
		var namespaceName = type.ContainingNamespace.IsGlobalNamespace
			? null
			: type.ContainingNamespace.ToDisplayString();

		return new EnumMatchModel(
			Name: enumName,
			Namespace: namespaceName,
			Accessibility: accessibility,
			Members: [.. members.Select(x => x.Name)]
		);
	}

	private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
	{
		while (type.BaseType is not null)
		{
			if (SymbolEqualityComparer.Default.Equals(type.BaseType, baseType))
			{
				return true;
			}

			type = type.BaseType;
		}
		return false;
	}

	private static IEnumerable<INamedTypeSymbol> GetDerivedTypes(
		INamedTypeSymbol baseType,
		Compilation compilation
	)
	{
		foreach (var tree in compilation.SyntaxTrees)
		{
			var model = compilation.GetSemanticModel(tree);

			var types = tree.GetRoot()
				.DescendantNodes()
				.OfType<TypeDeclarationSyntax>();

			foreach (var t in types)
			{
				if (model.GetDeclaredSymbol(t) is INamedTypeSymbol symbol)
				{
					if (IsDerivedFrom(symbol, baseType))
					{
						yield return symbol;
					}
				}
			}
		}
	}

	private static UnionMatchModel CreateUnionModel(INamedTypeSymbol type, Compilation compilation, string accessibility)
	{
		// Preserve source-traversal order (syntax tree order, then declaration order
		// within each tree) instead of sorting by name, mirroring the enum path so a
		// newly added derived type tends to land at the end of the parameter list.
		// Note: because derived types can be spread across files, this is less strictly
		// append-stable than the enum case — named arguments remain the safe call style.
		var derived = GetDerivedTypes(type, compilation)
			.Where(x => !x.IsAbstract)
			.ToList();

		var baseName = type.Name;
		var namespaceName = type.ContainingNamespace.IsGlobalNamespace
			? null
			: type.ContainingNamespace.ToDisplayString();

		return new UnionMatchModel(
			Name: baseName,
			Namespace: namespaceName,
			Accessibility: accessibility,
			DerivedTypes: [.. derived.Select(x => x.Name)]
		);
	}

	private static MatchModel CreateModel(INamedTypeSymbol type, Compilation compilation, string accessibility)
	{
		return type.TypeKind == TypeKind.Enum
			? CreateEnumModel(type, accessibility)
			: CreateUnionModel(type, compilation, accessibility);
	}

	public static IEnumerable<MatchModel> Create(Compilation compilation, ImmutableArray<INamedTypeSymbol> targets)
	{
		foreach (var type in targets.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>())
		{
			yield return CreateModel(type, compilation, GetEffectiveAccessibility(type));
		}
	}

	// For types targeted by [assembly: GenerateMatchFor(typeof(T))]. Generated as internal:
	// the extension is a local convenience in your assembly, not public API on a type you don't own.
	public static MatchModel CreateFor(Compilation compilation, INamedTypeSymbol type)
	{
		return CreateModel(type, compilation, accessibility: "internal");
	}
}
