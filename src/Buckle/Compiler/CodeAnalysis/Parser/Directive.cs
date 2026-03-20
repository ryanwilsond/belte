
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal class Directive {
    private readonly DirectiveTriviaSyntax _node;

    internal Directive(DirectiveTriviaSyntax node) {
        _node = node;
    }

    internal SyntaxKind kind => _node.kind;
}
