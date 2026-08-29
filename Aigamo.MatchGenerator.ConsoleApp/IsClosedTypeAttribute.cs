namespace System.Runtime.CompilerServices;

// Polyfill for the attribute the C# 15 `closed` modifier lowers to. The compiler emits
// [IsClosedType] on a closed type and requires the type (with a parameterless constructor)
// to exist; targeting a framework that predates it means providing it ourselves.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class IsClosedTypeAttribute : Attribute;
