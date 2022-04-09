
namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class Parameter : Node {
        public Token typeName { get; }
        public Token identifier { get; }
        public override SyntaxType type => SyntaxType.PARAMETER;

        public Parameter(SyntaxTree syntaxTree, Token typeName_, Token identifier_) : base(syntaxTree) {
            typeName = typeName_;
            identifier = identifier_;
        }
    }

    internal sealed class FunctionDeclaration : Member {
        public Token typeName { get; }
        public Token identifier { get; }
        public Token openParenthesis { get; }
        public SeparatedSyntaxList<Parameter> parameters { get; }
        public Token closeParenthesis { get; }
        public BlockStatement body { get; }
        public override SyntaxType type => SyntaxType.FUNCTION_DECLARATION;

        public FunctionDeclaration(
            SyntaxTree syntaxTree, Token typeName_, Token identifier_, Token openParenthesis_,
            SeparatedSyntaxList<Parameter> parameters_, Token closeParenthesis_, BlockStatement body_)
            : base(syntaxTree) {
            typeName = typeName_;
            identifier = identifier_;
            openParenthesis = openParenthesis_;
            parameters = parameters_;
            closeParenthesis = closeParenthesis_;
            body = body_;
        }
    }
}
