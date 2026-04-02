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
		});

		var targets = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"Aigamo.MatchGenerator.GenerateMatchAttribute",
				static (node, _) => node is TypeDeclarationSyntax or EnumDeclarationSyntax,
				static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol
			);

		var compilationAndTargets = context.CompilationProvider.Combine(targets.Collect());

		context.RegisterSourceOutput(compilationAndTargets, static (spc, source) =>
		{
			var (compilation, types) = source;

			var models = MatchModelFactory.Create(compilation, types);

			foreach (var model in models)
			{
				var code = MatchCodeGenerator.Generate(model);
				spc.AddSource(model.HintName, code);
			}
		});
	}
}
