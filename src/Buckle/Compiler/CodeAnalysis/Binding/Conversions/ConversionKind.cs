
namespace Buckle.CodeAnalysis.Binding;

internal enum ConversionKind : byte {
    None,
    DefaultLiteral,
    NullLiteral,
    Identity,
    Implicit,
    ImplicitNullable,
    ImplicitReference,
    ImplicitConstant,
    ImplicitUserDefined,
    AnyBoxing,
    AnyBoxingImplicitNullable,
    Explicit,
    ExplicitNullable,
    ExplicitReference,
    ExplicitUserDefined,
    AnyBoxingExplicitNullable,
    AnyUnboxing,
    AnyUnboxingImplicitNullable,
    AnyUnboxingExplicitNullable,
    ListExpression,
}
