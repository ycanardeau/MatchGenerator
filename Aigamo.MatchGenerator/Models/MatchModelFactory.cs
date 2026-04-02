using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aigamo.MatchGenerator.Models;

static file class AccessibilityMatchExtensions
{
	public static U Match<U>(
		this Accessibility value,
		Func<U> onNotApplicable,
		Func<U> onPrivate,
		Func<U> onProtectedAndInternal,
		Func<U> onProtected,
		Func<U> onInternal,
		Func<U> onProtectedOrInternal,
		Func<U> onPublic
	)
	{
		return value switch
		{
			Accessibility.NotApplicable => onNotApplicable(),
			Accessibility.Private => onPrivate(),
			Accessibility.ProtectedAndInternal => onProtectedAndInternal(),
			Accessibility.Protected => onProtected(),
			Accessibility.Internal => onInternal(),
			Accessibility.ProtectedOrInternal => onProtectedOrInternal(),
			Accessibility.Public => onPublic(),
			_ => throw new UnreachableException(),
		};
	}
}

internal static class MatchModelFactory
{
	private static Accessibility Min(Accessibility left, Accessibility right)
	{
		return (Accessibility)Math.Min((int)left, (int)right);
	}

	private static string ToCode(Accessibility accessibility)
	{
		return accessibility.Match(
			onNotApplicable: () => "internal",
			onPrivate: () => "private",
			onProtectedAndInternal: () => "private protected",
			onProtected: () => "protected",
			onInternal: () => "internal",
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

	private static EnumMatchModel CreateEnumModel(INamedTypeSymbol type)
	{
		var members = type.GetMembers()
			.OfType<IFieldSymbol>()
			.Where(x => x.ConstantValue is not null)
			.OrderBy(x => x.Name)
			.ToList();

		var enumName = type.Name;
		var namespaceName = type.ContainingNamespace.IsGlobalNamespace
			? null
			: type.ContainingNamespace.ToDisplayString();

		var accessibility = GetEffectiveAccessibility(type);

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

	private static UnionMatchModel CreateUnionModel(INamedTypeSymbol type, Compilation compilation)
	{
		var derived = GetDerivedTypes(type, compilation)
			.Where(x => !x.IsAbstract)
			.OrderBy(x => x.Name)
			.ToList();

		var baseName = type.Name;
		var namespaceName = type.ContainingNamespace.IsGlobalNamespace
			? null
			: type.ContainingNamespace.ToDisplayString();

		var accessibility = GetEffectiveAccessibility(type);

		return new UnionMatchModel(
			Name: baseName,
			Namespace: namespaceName,
			Accessibility: accessibility,
			DerivedTypes: [.. derived.Select(x => x.Name)]
		);
	}

	public static IEnumerable<MatchModel> Create(Compilation compilation, ImmutableArray<INamedTypeSymbol> targets)
	{
		foreach (var type in targets.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>())
		{
			yield return type.TypeKind == TypeKind.Enum
				? CreateEnumModel(type)
				: CreateUnionModel(type, compilation);
		}
	}
}
