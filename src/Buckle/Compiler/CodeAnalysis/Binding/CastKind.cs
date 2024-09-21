
namespace Buckle.CodeAnalysis.Binding;

internal enum CastKind : byte {
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
