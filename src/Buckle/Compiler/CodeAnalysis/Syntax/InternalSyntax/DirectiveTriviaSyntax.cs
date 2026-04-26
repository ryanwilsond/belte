
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class DirectiveTriviaSyntax {
    internal sealed override bool isDirective => true;

    internal override DirectiveStack ApplyDirectives(DirectiveStack stack) {
        return stack.Add(new Directive(this));
    }
}
