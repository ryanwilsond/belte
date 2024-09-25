
namespace Buckle.CodeAnalysis.Binding;

internal enum ConversionKind : byte {
    None,
    Identity,
    Implicit,
    ImplicitNullable,
    ImplicitReference,
    Boxing,
    BoxingImplicitNullable,
    AnyBoxing,
    AnyBoxingImplicitNullable,
    Explicit,
    ExplicitNullable,
    ExplicitReference,
    BoxingExplicitNullable,
    Unboxing,
    UnboxingImplicitNullable,
    UnboxingExplicitNullable,
    AnyBoxingExplicitNullable,
    AnyUnboxing,
    AnyUnboxingImplicitNullable,
    AnyUnboxingExplicitNullable,
}
