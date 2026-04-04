
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class DirectiveStack {
    internal static readonly DirectiveStack Empty = new DirectiveStack(ConsList<Directive>.Empty);

    private readonly ConsList<Directive> _directives;

    private DirectiveStack(ConsList<Directive> directives) {
        _directives = directives;
    }

    internal DefineState IsDefined(string id) {
        for (var current = _directives; current is not null && current.Any(); current = current.tail) {

            switch (current.head.kind) {
                case SyntaxKind.DefineDirectiveTrivia:
                    if (current.head.GetIdentifier() == id)
                        return DefineState.Defined;

                    break;
                case SyntaxKind.UndefDirectiveTrivia:
                    if (current.head.GetIdentifier() == id)
                        return DefineState.Undefined;

                    break;
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                    do {
                        current = current.tail;

                        if (current is null || !current.Any())
                            return DefineState.Unspecified;
                    } while (current.head.kind != SyntaxKind.IfDirectiveTrivia);

                    break;
            }
        }

        return DefineState.Unspecified;
    }

    internal DirectiveStack Add(Directive directive) {
        return new DirectiveStack(new ConsList<Directive>(directive, _directives ?? ConsList<Directive>.Empty));
    }

    internal bool HasUnfinishedIf() {
        var prev = GetPreviousIfElifElseOrRegion(_directives);
        return prev != null && prev.Any();
    }

    internal bool HasPreviousIfOrElif() {
        var prev = GetPreviousIfElifElseOrRegion(_directives);
        return prev is not null && prev.Any() &&
            (prev.head.kind is SyntaxKind.IfDirectiveTrivia or SyntaxKind.ElifDirectiveTrivia);
    }

    internal bool PreviousBranchTaken() {
        for (var current = _directives; current is not null && current.Any(); current = current.tail) {
            if (current.head.branchTaken) {
                return true;
            } else if (current.head.kind == SyntaxKind.IfDirectiveTrivia) {
                return false;
            }
        }

        return false;
    }

    private static ConsList<Directive>? GetPreviousIfElifElseOrRegion(ConsList<Directive> directives) {
        var current = directives;

        while (current is not null && current.Any()) {
            switch (current.head.kind) {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                    return current;
            }

            current = current.tail;
        }

        return current;
    }
}
