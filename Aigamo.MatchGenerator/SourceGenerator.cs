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
		// Carry the attribute location so a bad target can be reported with a squiggle.
		var externalTargets = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"Aigamo.MatchGenerator.GenerateMatchForAttribute",
				static (_, _) => true,
				static (ctx, _) =>
				{
					var builder = ImmutableArray.CreateBuilder<(INamedTypeSymbol Type, Location Location)>();
					foreach (var attribute in ctx.Attributes)
					{
						if (attribute.ConstructorArguments.Length > 0 &&
							attribute.ConstructorArguments[0].Value is INamedTypeSymbol type)
						{
							var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
								?? Location.None;
							builder.Add((type, location));
						}
					}
					return builder.ToImmutable();
				}
			);

		var compilationAndTargets = context.CompilationProvider
			.Combine(targets.Collect())
			.Combine(externalTargets.Collect());

		context.RegisterSourceOutput(compilationAndTargets, static (spc, source) =>
		{
			var ((compilation, ownedTypes), externalGroups) = source;

			var produced = new HashSet<string>();

			foreach (var model in MatchModelFactory.Create(compilation, ownedTypes))
			{
				spc.AddSource(model.HintName, MatchCodeGenerator.Generate(model));
				produced.Add(model.HintName);
			}

			foreach (var (type, location) in externalGroups.SelectMany(static x => x))
			{
				var model = MatchModelFactory.CreateFor(compilation, type);

				var hasCases = model is EnumMatchModel { Members.Length: > 0 }
					or UnionMatchModel { DerivedTypes.Length: > 0 };
				if (!hasCases)
				{
					spc.ReportDiagnostic(Diagnostic.Create(
						Diagnostics.UnsupportedGenerateMatchForTarget,
						location,
						type.ToDisplayString()));
					continue;
				}

				// Skip if an annotated type (or an earlier target) already produced this file.
				if (produced.Add(model.HintName))
				{
					spc.AddSource(model.HintName, MatchCodeGenerator.Generate(model));
				}
			}
		});
	}
}
