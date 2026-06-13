using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundConditionalGotoStatement {
    internal BoundConditionalGotoStatement(
        SyntaxNode syntax,
        LabelSymbol label,
        BoundExpression condition,
        bool jumpIfTrue = true) : this(syntax, label, condition, jumpIfTrue, default, default) { }
}
