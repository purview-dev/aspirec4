// The C# 15 runtime types this package relies on.
//
// The package ships net10.0 and net11.0. `closed` and `union` were introduced in C# 15 alongside
// the .NET 11 runtime that carries their supporting types, so the net10.0 leg - the LTS, and the
// framework an Aspire AppHost targets - needs them declared here; the net11.0 leg uses System.Runtime's.
// Roslyn looks these types up by full name, so an internal declaration is recognised by the compiler
// for this assembly *and* by a consumer compiling against it.
//
// Keep the shapes identical to System.Runtime's: IsClosedTypeAttribute is populated with the fixed
// set of direct descendants, which is what makes a switch over the hierarchy exhaustive from
// another assembly. Delete this file if the net10.0 leg is ever dropped.

#if !NET11_0_OR_GREATER
#pragma warning disable IDE0130 // The namespace is fixed by the compiler's well-known type lookup.
namespace System.Runtime.CompilerServices;

#pragma warning restore IDE0130

/// <summary>Marks a class whose direct descendants are fixed to the declaring assembly.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
sealed class IsClosedTypeAttribute : Attribute
{
	/// <summary>The direct descendants the compiler found when the closed class was compiled.</summary>
	public Type[] DerivedTypes { get; set; } = [];
}
#endif
