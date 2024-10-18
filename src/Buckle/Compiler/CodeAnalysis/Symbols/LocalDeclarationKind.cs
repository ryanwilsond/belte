
namespace Buckle.CodeAnalysis.Symbols;

internal enum LocalDeclarationKind : byte {
    None,
    Variable,
    Constant,
    ConstantExpression,
}
