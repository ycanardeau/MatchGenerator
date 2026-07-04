# Aigamo.MatchGenerator

A Roslyn incremental source generator that emits exhaustive `Match` extension
methods for enums and union-like type hierarchies, plus analyzers that guard how
the generated API is used.

## Code style

### Method ordering (dependee-first)

Within a type, define a member *before* the members that depend on it: helpers
and lower-level methods first, the methods that call them afterwards. Read
top-to-bottom, a file introduces each name before it is used — the same
definition-before-use discipline that functional languages (F#, OCaml, ML)
enforce at the language level.

Concretely, the entry point tends to sit at the *bottom*, below the helpers it
orchestrates. For example, in `MatchNamedArgumentsAnalyzer` the `AnalyzeInvocation`
handler is defined before the `Initialize` method that registers it; in
`MatchModelFactory`, `IsDerivedFrom` → `GetDerivedTypes` → `CreateUnionModel` →
`CreateModel` → `Create` runs low-level to high-level down the file.
