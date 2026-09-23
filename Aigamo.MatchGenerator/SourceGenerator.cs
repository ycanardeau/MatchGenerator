using System.Collections.Immutable;
using Aigamo.MatchGenerator.Generators;
using Aigamo.MatchGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aigamo.MatchGenerator;

[Generator]
internal class SourceGenerator : IIncrementalGenerator
{
	// Empty by default (Red); set <MatchGeneratorParameterPrefix>on</MatchGeneratorParameterPrefix>
	// in the consumer's .csproj (or Directory.Build.props) for onRed. Declared as a
	// CompilerVisibleProperty in buildTransitive/Aigamo.MatchGenerator.props, which flows the
	// MSBuild property into GlobalOptions as build_property.<name> — genuinely one value for the
	// whole compilation, unlike a section-scoped .editorconfig entry.
	private static string GetParameterPrefix(AnalyzerConfigOptionsProvider provider)
	{
		return provider.GlobalOptions.TryGetValue(
			$"build_property.{Constants.ParameterPrefixPropertyName}",
			out var value
		)
			? value
			: "";
	}

	// The prefix is spliced directly into generated parameter names, so anything other than
	// a valid C# identifier (e.g. stray braces or a comment marker) could produce malformed
	// or unsafe generated code. Reject it instead and fall back to no prefix, rather than
	// skipping generation entirely: skipping would replace one clear AMG004 diagnostic with
	// a CS1061 "Match not found" at every call site, none of which point back to the actual
	// cause. Falling back keeps the method usable (IDE, IntelliSense) while AMG004's Error
	// severity still fails the build.
	private static string GetValidatedParameterPrefix(string prefix, SourceProductionContext spc)
	{
		if (prefix.Length == 0 || SyntaxFacts.IsValidIdentifier(prefix))
		{
			return prefix;
		}

		spc.ReportDiagnostic(
			Diagnostic.Create(Diagnostics.InvalidParameterPrefix, Location.None, prefix)
		);
		return "";
	}

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(ctx =>
		{
			ctx.AddSource(
				"GenerateMatchAttribute.g.cs",
				"""
				using System;

				namespace Aigamo.MatchGenerator;

				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
				internal sealed class GenerateMatchAttribute : Attribute;
				"""
			);

			ctx.AddSource(
				"GenerateMatchForAttribute.g.cs",
				"""
				using System;

				namespace Aigamo.MatchGenerator;

				[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
				internal sealed class GenerateMatchForAttribute(Type type) : Attribute
				{
					public Type Type { get; } = type;
				}
				"""
			);
		});

		// Types annotated in this compilation: [GenerateMatch] on the declaration.
		var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
			"Aigamo.MatchGenerator.GenerateMatchAttribute",
			static (node, _) => node is TypeDeclarationSyntax or EnumDeclarationSyntax,
			static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol
		);

		// Types you don't own, named by [assembly: GenerateMatchFor(typeof(T))].
		// Carry the attribute location so a bad target can be reported with a squiggle.
		var externalTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
			"Aigamo.MatchGenerator.GenerateMatchForAttribute",
			static (_, _) => true,
			static (ctx, _) =>
			{
				var builder = ImmutableArray.CreateBuilder<(
					INamedTypeSymbol Type,
					Location Location
				)>();
				foreach (var attribute in ctx.Attributes)
				{
					if (
						attribute.ConstructorArguments.Length > 0
						&& attribute.ConstructorArguments[0].Value is INamedTypeSymbol type
					)
					{
						var location =
							attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
							?? Location.None;
						builder.Add((type, location));
					}
				}
				return builder.ToImmutable();
			}
		);

		var compilationAndTargets = context
			.CompilationProvider.Combine(targets.Collect())
			.Combine(externalTargets.Collect())
			.Combine(context.AnalyzerConfigOptionsProvider);

		context.RegisterSourceOutput(
			compilationAndTargets,
			static (spc, source) =>
			{
				var (((compilation, ownedTypes), externalGroups), configOptions) = source;

				var parameterPrefix = GetValidatedParameterPrefix(
					GetParameterPrefix(configOptions),
					spc
				);

				var produced = new HashSet<string>();

				foreach (var model in MatchModelFactory.Create(compilation, ownedTypes))
				{
					spc.AddSource(
						model.HintName,
						MatchCodeGenerator.Generate(model, parameterPrefix)
					);
					produced.Add(model.HintName);
				}

				foreach (var (type, location) in externalGroups.SelectMany(static x => x))
				{
					var model = MatchModelFactory.CreateFor(compilation, type);

					var hasCases =
						model
						is MatchModel.Enum { Members.Length: > 0 }
							or MatchModel.Union { DerivedTypes.Length: > 0 };
					if (!hasCases)
					{
						spc.ReportDiagnostic(
							Diagnostic.Create(
								Diagnostics.UnsupportedGenerateMatchForTarget,
								location,
								type.ToDisplayString()
							)
						);
						continue;
					}

					// Skip if an annotated type (or an earlier target) already produced this file.
					if (produced.Add(model.HintName))
					{
						spc.AddSource(
							model.HintName,
							MatchCodeGenerator.Generate(model, parameterPrefix)
						);
					}
				}
			}
		);
	}
}
