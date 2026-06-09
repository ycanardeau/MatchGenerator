using System.Runtime.CompilerServices;
using Aigamo.MatchGenerator;
using Microsoft.CodeAnalysis;

[assembly: InternalsVisibleTo("Aigamo.MatchGenerator.Tests")]

[assembly: GenerateMatchFor(typeof(Accessibility))]
