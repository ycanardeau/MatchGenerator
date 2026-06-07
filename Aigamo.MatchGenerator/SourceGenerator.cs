using System.Collections.Immutable;
using Aigamo.MatchGenerator.Generators;
using Aigamo.MatchGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aigamo.MatchGenerator;

[Generator]
internal class SourceGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(ctx =>
		{
			ctx.AddSource("GenerateMatchAttribute.g.cs", """
				using System;

				namespace Aigamo.MatchGenerator;

				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
				internal sealed class GenerateMatchAttribute : Attribute;
				""");

			ctx.AddSource("GenerateMatchForAttribute.g.cs", """
				using System;

				namespace Aigamo.MatchGenerator;

				[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
				internal sealed class GenerateMatchForAttribute(Type type) : Attribute
				{
					public Type Type { get; } = type;
				}
				""");
		});

		// Types annotated in this compilation: [GenerateMatch] on the declaration.
		var targets = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"Aigamo.MatchGenerator.GenerateMatchAttribute",
				static (node, _) => node is TypeDeclarationSyntax or EnumDeclarationSyntax,
				static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol
			);

		// Types you don't own, named by [assembly: GenerateMatchFor(typeof(T))].
		var externalTargets = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"Aigamo.MatchGenerator.GenerateMatchForAttribute",
				static (_, _) => true,
				static (ctx, _) => ctx.Attributes
					.Select(static a => a.ConstructorArguments.Length > 0
						? a.ConstructorArguments[0].Value as INamedTypeSymbol
						: null)
					.OfType<INamedTypeSymbol>()
					.ToImmutableArray()
			);

		var compilationAndTargets = context.CompilationProvider
			.Combine(targets.Collect())
			.Combine(externalTargets.Collect());

		context.RegisterSourceOutput(compilationAndTargets, static (spc, source) =>
		{
			var ((compilation, types), externalTypes) = source;

			var allTypes = types
				.Concat(externalTypes.SelectMany(static x => (IEnumerable<INamedTypeSymbol>)x))
				.ToImmutableArray();

			var models = MatchModelFactory.Create(compilation, allTypes);

			foreach (var model in models)
			{
				var code = MatchCodeGenerator.Generate(model);
				spc.AddSource(model.HintName, code);
			}
		});
	}
}
