
namespace Buckle.CodeAnalysis.Binding;

internal enum ConversionKind : byte {
    None,
    Identity,
    Implicit,
    ImplicitNullable,
    ImplicitReference,
    AnyBoxing,
    AnyBoxingImplicitNullable,
    Explicit,
    ExplicitNullable,
    ExplicitReference,
    AnyBoxingExplicitNullable,
    AnyUnboxing,
    AnyUnboxingImplicitNullable,
    AnyUnboxingExplicitNullable,
}
