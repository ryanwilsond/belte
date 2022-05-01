
namespace Buckle.CodeAnalysis.Syntax;

internal sealed partial class Parameter : Node {
    public TypeClause typeClause { get; }
    public Token identifier { get; }
    public override SyntaxType type => SyntaxType.PARAMETER;

    public Parameter(SyntaxTree syntaxTree, TypeClause typeClause_, Token identifier_) : base(syntaxTree) {
        typeClause = typeClause_;
        identifier = identifier_;
    }
}

internal sealed partial class FunctionDeclaration : Member {
    public TypeClause returnType { get; }
    public Token identifier { get; }
    public Token openParenthesis { get; }
    public SeparatedSyntaxList<Parameter> parameters { get; }
    public Token closeParenthesis { get; }
    public BlockStatement body { get; }
    public override SyntaxType type => SyntaxType.FUNCTION_DECLARATION;

    public FunctionDeclaration(
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
