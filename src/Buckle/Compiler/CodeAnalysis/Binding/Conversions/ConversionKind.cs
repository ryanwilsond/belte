
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
    ImplicitNumeric,
    AnyBoxing,
    Explicit,
    ExplicitNullable,
    ExplicitReference,
    ExplicitUserDefined,
    ExplicitPointerToPointer,
    ExplicitIntegerToPointer,
    ExplicitPointerToInteger,
    ExplicitNumeric,
    AnyUnboxing,
    ListExpression,
}
