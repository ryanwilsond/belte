
namespace Buckle.CodeAnalysis.Symbols;

internal enum DataContainerDeclarationKind : byte {
    None,
    Variable,
    Constant,
    Final,
    ConstantExpression,
    ForEachLocal,
    NullBindingLocal,
    ScopedLocal,
    PatternLocal,
    OutVariable,
    DeclarationExpressionVariable,
}
