
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class DirectiveStack {
    internal static readonly DirectiveStack Empty = new DirectiveStack(ConsList<Directive>.Empty);

    private readonly ConsList<Directive> _directives;

    private DirectiveStack(ConsList<Directive> directives) {
        _directives = directives;
    }

    internal DirectiveStack Add(Directive directive) {
        return new DirectiveStack(new ConsList<Directive>(directive, _directives ?? ConsList<Directive>.Empty));
    }
}
