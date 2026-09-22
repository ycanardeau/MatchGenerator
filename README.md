# MatchGenerator

**Bring exhaustive pattern matching to C# enums and unions with zero boilerplate.**

[![NuGet](https://img.shields.io/nuget/v/Aigamo.MatchGenerator.svg)](https://www.nuget.org/packages/Aigamo.MatchGenerator)

[MatchGenerator](https://github.com/ycanardeau/MatchGenerator) is a Roslyn source generator that creates `Match` extension methods for your enums and discriminated-union-like types, enabling concise, expressive, and compile-time safe branching.

## Features

- Generate `Match` extension methods for enums and unions
- Exhaustive by design (no missing cases)
- Attribute-driven (opt-in per type)
- Works with external types you don't own (via `[assembly: GenerateMatchFor(typeof(T))]`)
- Supports generic base types (`Option<T>`, `Either<L, R>`, recursive `List<T>`, …)
- Respects effective accessibility
- Zero runtime cost (pure source generation)

## Getting Started

### 1. Install the package

```bash
dotnet add package Aigamo.MatchGenerator
```

> **Tip:** To share the reference across every project in a directory, add a `Directory.Build.props`. Combined with a global `Using`, this also drops the need for a per-file `using Aigamo.MatchGenerator;`:
>
> ```xml
> <Project>
>
>   <ItemGroup>
>     <PackageReference Include="Aigamo.MatchGenerator" />
>     <Using Include="Aigamo.MatchGenerator" />
>   </ItemGroup>
>
> </Project>
> ```

### 2. Annotate your type

#### Enum example

```csharp
using Aigamo.MatchGenerator;

[GenerateMatch]
public enum Gender
{
	Male = 1,
	Female,
}
```

#### Union example

```csharp
using Aigamo.MatchGenerator;

[GenerateMatch]
abstract record MaritalStatus
{
	private MaritalStatus() { }

	public sealed record Single : MaritalStatus;
	public sealed record Married : MaritalStatus;
	public sealed record Divorced : MaritalStatus;
	public sealed record Widowed : MaritalStatus;
}
```

Nesting the cases inside the base type and giving it a `private` constructor makes the hierarchy **closed** — no case can be declared outside `MaritalStatus`. The generator qualifies the nested cases by their containing type (`MaritalStatus.Single`, …) so the generated extension resolves them correctly. Top-level derived types work too; nesting is just a common way to model a closed union.

On C# 15 and later, prefer the language-native [`closed` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/closed) to constrain the hierarchy directly:

```csharp
[GenerateMatch]
public closed record MaritalStatus
{
	public sealed record Single : MaritalStatus;
	public sealed record Married : MaritalStatus;
	public sealed record Divorced : MaritalStatus;
	public sealed record Widowed : MaritalStatus;
}
```

The package ships an analyzer (**AMG003**) that reports a warning when a `[GenerateMatch]` base type is not `closed`: an open hierarchy can gain a derived type the generated `Match` never handles, silently dropping a case. The warning only fires when the compiler supports `closed` (C# 15+), so earlier language versions are unaffected. A quick fix ("Mark base type as closed") adds the `closed` modifier for you. Relax or disable it in `.editorconfig`:

```ini
dotnet_diagnostic.AMG003.severity = suggestion
```

#### Generic union example

The base type can be generic. Its type parameters flow through to the generated `Match` method, which appends the result type parameter (`U`) after them:

```csharp
using Aigamo.MatchGenerator;

[GenerateMatch]
closed record Option<T>
{
	private Option() { }

	public sealed record Some(T Value) : Option<T>;
	public sealed record None : Option<T>;
}
```

Multiple type parameters (`Either<L, R>`) and recursive definitions (`List<T>` whose `Cons` case holds a `List<T>` tail) work the same way — see the [generated code](#generic-union) below.

#### External type example

If the enum or union lives in another assembly — so you can't put `[GenerateMatch]` on it — target it by `typeof` with an assembly-level attribute instead:

```csharp
using Aigamo.MatchGenerator;

[assembly: GenerateMatchFor(typeof(DayOfWeek))]
```

Then call `Match` exactly as you would on an annotated type:

```csharp
var label = today.Match(
	Sunday: () => "Sun",
	Monday: () => "Mon",
	Tuesday: () => "Tue",
	Wednesday: () => "Wed",
	Thursday: () => "Thu",
	Friday: () => "Fri",
	Saturday: () => "Sat"
);
```

The generated method is placed in the target type's namespace and is `internal` to your assembly (a local convenience, not public API on a type you don't own). Repeat the attribute (it allows multiple) to target several types. This works for external **enums**, and for unions whose derived types are declared in your own code (cross-assembly derived types are not discovered).

If a target has nothing to match — it isn't an enum and has no derived types in your compilation — the generator reports `AMG001` and skips it.

### 3. Use `Match`

#### Enum

```csharp
var message = gender.Match(
	Male: () => "male",
	Female: () => "female"
);
```

#### Union

```csharp
var message = maritalStatus.Match(
	Single: x => "single",
	Married: x => "married",
	Divorced: x => "divorced",
	Widowed: x => "widowed"
);
```

## Why use MatchGenerator?

### Without MatchGenerator

#### Enum

```csharp
var message = gender switch
{
	Gender.Male => "male",
	Gender.Female => "female",
	_ => throw new UnreachableException(),
};
```

#### Union

```csharp
var message = maritalStatus switch
{
	MaritalStatus.Single x => "single",
	MaritalStatus.Married x => "married",
	MaritalStatus.Divorced x => "divorced",
	MaritalStatus.Widowed x => "widowed",
	_ => throw new UnreachableException(),
};
```

### With MatchGenerator

```csharp
var message = gender.Match(
	Male: () => "male",
	Female: () => "female"
);
```

- More concise
- More readable
- No default case required
- Compile-time safety

## Exhaustiveness Guarantee

All cases must be handled.

If a new enum value or union type is added:

```csharp
public enum Gender
{
	Male = 1,
	Female,
	Other,
}
```

or

```csharp
public sealed record Separated : MaritalStatus;
```

Existing `Match` calls will fail to compile until updated. This ensures no cases are missed.

## Parameter Order

The `Match` parameters follow **declaration order** — the order in which the enum members (or union derived types) appear in source — not alphabetical order. This keeps the parameter list append-stable: a case added at the end of the enum becomes the last parameter, so existing **positional** call sites keep compiling.

Two caveats:

- Inserting or reordering cases in the middle still shifts positions and can silently rebind positional arguments.
- For unions, derived types can be spread across files, so "declaration order" is really source-traversal order and is less strictly append-stable.

Because of this, prefer **named arguments** (`Single:`, `Married:`, …) — they are order-independent and the only fully safe call style across changes. The generated parameter names are designed for exactly this.

To enforce it, the package ships an analyzer (**AMG002**) that reports an error when `Match` is called with positional arguments. If you'd rather have it as a warning (or turn it off entirely), relax it in `.editorconfig`:

```ini
dotnet_diagnostic.AMG002.severity = warning
```

## Parameter Names

By default, generated parameters are named after the case they handle (`Male`, `Single`, …). If you prefer the classic `onFoo` style, set a prefix in `.editorconfig`:

```ini
matchgenerator_parameter_prefix = on
```

```csharp
var message = gender.Match(
	onMale: () => "male",
	onFemale: () => "female"
);
```

The prefix applies project-wide and defaults to empty.

## Performance

`Match` is a thin wrapper around a `switch`, and the generated method is marked
`[MethodImpl(MethodImplOptions.AggressiveInlining)]` so the JIT can fold the dispatch into the
call site. The only runtime cost worth thinking about is the `Func<>` callbacks:

- **Prefer non-capturing (`static`) lambdas.** A lambda that captures no local state compiles to a
  single cached delegate — zero allocation per call. Marking them `static` makes the compiler
  enforce that:

  ```csharp
  var message = gender.Match(
      Male: static () => "male",
      Female: static () => "female"
  );
  ```

- **A capturing lambda allocates a closure per call.** `x => x + local` builds a new closure object
  (and delegates) on every `Match` invocation. That is usually fine, but on a hot path it shows up.
  Inlining lets the JIT's escape analysis stack-allocate most of it, but avoiding the capture is
  strictly cheaper.

The repository includes a [BenchmarkDotNet](https://benchmarkdotnet.org/) project
(`Aigamo.MatchGenerator.Benchmarks`) that measures this. Run it with:

```bash
dotnet run -c Release --project Aigamo.MatchGenerator.Benchmarks
```

## Generated Code (Example)

### Enum

```csharp
internal static class GenderMatchExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static U Match<U>(
		this Gender value,
		Func<U> Male,
		Func<U> Female
	)
	{
		return value switch
		{
			Gender.Male => Male(),
			Gender.Female => Female(),
			_ => throw new UnreachableException(),
		};
	}
}
```

### Union

```csharp
internal static class MaritalStatusMatchExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static U Match<U>(
		this MaritalStatus value,
		Func<MaritalStatus.Single, U> Single,
		Func<MaritalStatus.Married, U> Married,
		Func<MaritalStatus.Divorced, U> Divorced,
		Func<MaritalStatus.Widowed, U> Widowed
	)
	{
		return value switch
		{
			MaritalStatus.Single x => Single(x),
			MaritalStatus.Married x => Married(x),
			MaritalStatus.Divorced x => Divorced(x),
			MaritalStatus.Widowed x => Widowed(x),
			_ => throw new UnreachableException(),
		};
	}
}
```

### Generic union

The base type's type parameters are declared on the method, ahead of the result type `U`, and the cases are qualified by the constructed base (`Option<T>.Some`):

```csharp
internal static class OptionMatchExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static U Match<T, U>(
		this Option<T> value,
		Func<Option<T>.Some, U> Some,
		Func<Option<T>.None, U> None
	)
	{
		return value switch
		{
			Option<T>.Some x => Some(x),
			Option<T>.None x => None(x),
			_ => throw new UnreachableException(),
		};
	}
}
```

Multiple type parameters carry through in declaration order — `Either<L, R>` produces `Match<L, R, U>` — and recursive definitions such as `List<T>` are handled without special-casing:

```csharp
internal static class ListMatchExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static U Match<T, U>(
		this List<T> value,
		Func<List<T>.Empty, U> Empty,
		Func<List<T>.Cons, U> Cons
	)
	{
		return value switch
		{
			List<T>.Empty x => Empty(x),
			List<T>.Cons x => Cons(x),
			_ => throw new UnreachableException(),
		};
	}
}
```

## References

- [Introducing C# Source Generators - .NET Blog](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/)
- [roslyn/docs/features/source-generators.cookbook.md at main · dotnet/roslyn](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
- [roslyn/docs/features/incremental-generators.cookbook.md at main · dotnet/roslyn](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md)
- [Domain Modeling Made Functional: Tackle Software Complexity with Domain-Driven Design and F# by Scott Wlaschin](https://pragprog.com/titles/swdddf/domain-modeling-made-functional/)
- [It Seems the C# Team Is Finally Considering Supporting Discriminated Unions - DEV Community](https://dev.to/canro91/it-seems-the-c-team-is-finally-considering-supporting-discriminated-unions-59k3)
- [salvois/DiscriminatedOnions: A stinky but tasty hack to emulate F#-like discriminated unions in C#](https://github.com/salvois/DiscriminatedOnions)
