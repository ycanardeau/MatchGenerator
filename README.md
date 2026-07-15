# MatchGenerator

**Bring exhaustive pattern matching to C# enums and unions with zero boilerplate.**

[MatchGenerator](https://github.com/ycanardeau/MatchGenerator) is a Roslyn source generator that creates `Match` extension methods for your enums and discriminated-union-like types, enabling concise, expressive, and compile-time safe branching.

## Features

- Generate `Match` extension methods for enums and unions
- Exhaustive by design (no missing cases)
- Attribute-driven (opt-in per type)
- Works with external types you don't own (via `[assembly: GenerateMatchFor(typeof(T))]`)
- Supports generics (`Match<U>`)
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
abstract record MaritalStatus;

sealed record Single : MaritalStatus;
sealed record Married : MaritalStatus;
sealed record Divorced : MaritalStatus;
sealed record Widowed : MaritalStatus;
```

#### External type example

If the enum or union lives in another assembly — so you can't put `[GenerateMatch]` on it — target it by `typeof` with an assembly-level attribute instead:

```csharp
using Aigamo.MatchGenerator;

[assembly: GenerateMatchFor(typeof(DayOfWeek))]
```

Then call `Match` exactly as you would on an annotated type:

```csharp
var label = today.Match(
	onSunday: () => "Sun",
	onMonday: () => "Mon",
	onTuesday: () => "Tue",
	onWednesday: () => "Wed",
	onThursday: () => "Thu",
	onFriday: () => "Fri",
	onSaturday: () => "Sat"
);
```

The generated method is placed in the target type's namespace and is `internal` to your assembly (a local convenience, not public API on a type you don't own). Repeat the attribute (it allows multiple) to target several types. This works for external **enums**, and for unions whose derived types are declared in your own code (cross-assembly derived types are not discovered).

If a target has nothing to match — it isn't an enum and has no derived types in your compilation — the generator reports `AMG001` and skips it.

### 3. Use `Match`

#### Enum

```csharp
var message = gender.Match(
	onMale: () => "male",
	onFemale: () => "female"
);
```

#### Union

```csharp
var message = maritalStatus.Match(
	onSingle: x => "single",
	onMarried: x => "married",
	onDivorced: x => "divorced",
	onWidowed: x => "widowed"
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
	Single x => "single",
	Married x => "married",
	Divorced x => "divorced",
	Widowed x => "widowed",
	_ => throw new UnreachableException(),
};
```

### With MatchGenerator

```csharp
var message = gender.Match(
	onMale: () => "male",
	onFemale: () => "female"
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
sealed record Separated : MaritalStatus;
```

Existing `Match` calls will fail to compile until updated. This ensures no cases are missed.

## Parameter Order

The `Match` parameters follow **declaration order** — the order in which the enum members (or union derived types) appear in source — not alphabetical order. This keeps the parameter list append-stable: a case added at the end of the enum becomes the last parameter, so existing **positional** call sites keep compiling.

Two caveats:

- Inserting or reordering cases in the middle still shifts positions and can silently rebind positional arguments.
- For unions, derived types can be spread across files, so "declaration order" is really source-traversal order and is less strictly append-stable.

Because of this, prefer **named arguments** (`onSingle:`, `onMarried:`, …) — they are order-independent and the only fully safe call style across changes. The generated parameter names are designed for exactly this.

To enforce it, the package ships an analyzer (**AMG002**) that reports an error when `Match` is called with positional arguments. If you'd rather have it as a warning (or turn it off entirely), relax it in `.editorconfig`:

```ini
dotnet_diagnostic.AMG002.severity = warning
```

## Generated Code (Example)

### Enum

```csharp
internal static class GenderMatchExtensions
{
	public static U Match<U>(
		this Gender value,
		Func<U> onMale,
		Func<U> onFemale
	)
	{
		return value switch
		{
			Gender.Male => onMale(),
			Gender.Female => onFemale(),
			_ => throw new UnreachableException(),
		};
	}
}
```

### Union

```csharp
internal static class MaritalStatusMatchExtensions
{
	public static U Match<U>(
		this MaritalStatus value,
		Func<Single, U> onSingle,
		Func<Married, U> onMarried,
		Func<Divorced, U> onDivorced,
		Func<Widowed, U> onWidowed
	)
	{
		return value switch
		{
			Single x => onSingle(x),
			Married x => onMarried(x),
			Divorced x => onDivorced(x),
			Widowed x => onWidowed(x),
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
