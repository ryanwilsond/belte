
namespace Buckle.CodeAnalysis.Symbols;

internal enum DataContainerDeclarationKind : byte {
    None,
    Variable,
    Constant,
    Final,
    ConstantExpression,
    ForEachLocal,
    ConstantForEachLocal,
    ConstantNullBindingLocal,
    NullBindingLocal,
    ScopedLocal,
    PatternLocal,
    OutVariable,
    DeclarationExpressionVariable,
}
