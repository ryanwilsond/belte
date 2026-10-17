using System.Runtime.CompilerServices;
using System.Threading;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal readonly struct DirectiveStack {
    internal static readonly DirectiveStack Empty = new DirectiveStack(ConsList<Directive>.Empty);

    private readonly ConsList<Directive> _directives;

    private DirectiveStack(ConsList<Directive> directives) {
        _directives = directives;
    }

    internal bool isNull => _directives is null;

    internal static void InterlockedInitialize(ref DirectiveStack location, DirectiveStack value) {
        Interlocked.CompareExchange(ref Unsafe.AsRef(in location._directives), value._directives, null);
    }

    internal bool IncrementallyEquivalent(DirectiveStack other) {
        var mine = SkipInsignificantDirectives(_directives);
        var theirs = SkipInsignificantDirectives(other._directives);
        var mineHasAny = mine is not null && mine.Any();
        var theirsHasAny = theirs is not null && theirs.Any();

        while (mineHasAny && theirsHasAny) {
            if (!mine.head.IncrementallyEquivalent(theirs.head))
                return false;

            mine = SkipInsignificantDirectives(mine.tail);
            theirs = SkipInsignificantDirectives(theirs.tail);
            mineHasAny = mine is not null && mine.Any();
            theirsHasAny = theirs is not null && theirs.Any();
        }

        return mineHasAny == theirsHasAny;
    }

    private static ConsList<Directive> SkipInsignificantDirectives(ConsList<Directive> directives) {
        for (; directives is not null && directives.Any(); directives = directives.tail) {
            switch (directives.head.kind) {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.ElseDirectiveTrivia:
                case SyntaxKind.EndIfDirectiveTrivia:
                case SyntaxKind.DefineDirectiveTrivia:
                case SyntaxKind.UndefDirectiveTrivia:
                    return directives;
            }
        }

        return directives;
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
        switch (directive.kind) {
            case SyntaxKind.EndIfDirectiveTrivia:
                var prevIf = GetPreviousIf(_directives);

                if (prevIf is null || !prevIf.Any())
                    goto default;

                return new DirectiveStack(CompleteIf(_directives, out _));
            default:
                return new DirectiveStack(new ConsList<Directive>(directive, _directives ?? ConsList<Directive>.Empty));
        }
    }

    internal bool HasUnfinishedIf() {
        var prev = GetPreviousIfElifElseOrRegion(_directives);
        return prev is not null && prev.Any();
    }

    internal bool HasPreviousIfOrElif() {
        var prev = GetPreviousIfElifElseOrRegion(_directives);

        return prev is not null && prev.Any() &&
            (prev.head.kind is SyntaxKind.IfDirectiveTrivia or SyntaxKind.ElifDirectiveTrivia);
    }

    internal bool PreviousBranchTaken() {
        for (var current = _directives; current is not null && current.Any(); current = current.tail) {
            if (current.head.branchTaken)
                return true;
            else if (current.head.kind == SyntaxKind.IfDirectiveTrivia)
                return false;
        }

        return false;
    }


    private static ConsList<Directive> CompleteIf(ConsList<Directive> stack, out bool include) {
        if (!stack.Any()) {
            include = true;
            return stack;
        }

        if (stack.head.kind == SyntaxKind.IfDirectiveTrivia) {
            include = stack.head.branchTaken;
            return stack.tail;
        }

        var newStack = CompleteIf(stack.tail, out include);

        switch (stack.head.kind) {
            case SyntaxKind.ElifDirectiveTrivia:
            case SyntaxKind.ElseDirectiveTrivia:
                include = stack.head.branchTaken;
                break;
            default:
                if (include)
                    newStack = new ConsList<Directive>(stack.head, newStack);

                break;
        }

        return newStack;
    }

    private static ConsList<Directive> GetPreviousIf(ConsList<Directive> directives) {
        var current = directives;

        while (current is not null && current.Any()) {
            switch (current.head.kind) {
                case SyntaxKind.IfDirectiveTrivia:
                    return current;
            }

            current = current.tail;
        }

        return current;
    }

    private static ConsList<Directive> GetPreviousIfElifElseOrRegion(ConsList<Directive> directives) {
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
