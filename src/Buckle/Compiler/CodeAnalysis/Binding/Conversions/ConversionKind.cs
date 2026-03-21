
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
    ImplicitPointerToVoid,
    ImplicitNullToPointer,
    AnyBoxing,
    Explicit,
    ExplicitNullable,
    ExplicitReference,
    ExplicitUserDefined,
    ExplicitPointerToPointer,
    AnyUnboxing,
    ListExpression,
}
