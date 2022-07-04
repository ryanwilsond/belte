
namespace Buckle.CodeAnalysis.Syntax;

internal sealed partial class Parameter : Node {
    internal TypeClause typeClause { get; }
    internal Token identifier { get; }
    internal override SyntaxType type => SyntaxType.PARAMETER;

    internal Parameter(SyntaxTree syntaxTree, TypeClause typeClause_, Token identifier_) : base(syntaxTree) {
        typeClause = typeClause_;
        identifier = identifier_;
    }
}

internal sealed partial class FunctionDeclaration : Member {
    internal TypeClause returnType { get; }
    internal Token identifier { get; }
    internal Token openParenthesis { get; }
    internal SeparatedSyntaxList<Parameter> parameters { get; }
    internal Token closeParenthesis { get; }
    internal BlockStatement body { get; }
    internal override SyntaxType type => SyntaxType.FUNCTION_DECLARATION;

    internal FunctionDeclaration(
        SyntaxTree syntaxTree, TypeClause returnType_, Token identifier_, Token openParenthesis_,
        SeparatedSyntaxList<Parameter> parameters_, Token closeParenthesis_, BlockStatement body_)
        : base(syntaxTree) {
        returnType = returnType_;
        identifier = identifier_;
        openParenthesis = openParenthesis_;
        parameters = parameters_;
        closeParenthesis = closeParenthesis_;
        body = body_;
    }
}
