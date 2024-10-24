using System.Threading;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal struct LexicalSortKey {
    private int _treeOrdinal;
    private int _position;

    internal static readonly LexicalSortKey NotInSource = new LexicalSortKey() { _treeOrdinal = -1, _position = 0 };

    internal static readonly LexicalSortKey NotInitialized = new LexicalSortKey() { _treeOrdinal = -1, _position = -1 };

    internal static LexicalSortKey GetSynthesizedMemberKey(int offset)
        => new LexicalSortKey() { _treeOrdinal = int.MaxValue, _position = int.MaxValue - 2 - offset };

    internal static readonly LexicalSortKey SynthesizedCtor
        = new LexicalSortKey() { _treeOrdinal = int.MaxValue, _position = int.MaxValue - 1 };
    internal static readonly LexicalSortKey SynthesizedCCtor
        = new LexicalSortKey() { _treeOrdinal = int.MaxValue, _position = int.MaxValue };

    private LexicalSortKey(int treeOrdinal, int position) {
        _treeOrdinal = treeOrdinal;
        _position = position;
    }

    private LexicalSortKey(SyntaxTree tree, int position, Compilation compilation)
        : this(tree is null ? -1 : compilation.GetSyntaxTreeOrdinal(tree), position) { }

    internal LexicalSortKey(SyntaxReference syntaxRef, Compilation compilation)
        : this(syntaxRef.syntaxTree, syntaxRef.span.start, compilation) { }

    internal LexicalSortKey(BelteSyntaxNode node, Compilation compilation)
        : this(node.syntaxTree, node.span.start, compilation) { }

    internal LexicalSortKey(SyntaxToken token, Compilation compilation)
        : this(token.syntaxTree, token.span.start, compilation) { }

    internal readonly int treeOrdinal => _treeOrdinal;

    internal readonly int position => _position;

    internal bool isInitialized => Volatile.Read(ref _position) >= 0;

    internal static int Compare(LexicalSortKey x, LexicalSortKey y) {
        int comparison;

        if (x.treeOrdinal != y.treeOrdinal) {
            if (x.treeOrdinal < 0)
                return 1;
            else if (y.treeOrdinal < 0)
                return -1;

            comparison = x.treeOrdinal - y.treeOrdinal;
            return comparison;
        }

        return x.position - y.position;
    }

    internal static LexicalSortKey First(LexicalSortKey x, LexicalSortKey y) {
        var comparison = Compare(x, y);
        return comparison > 0 ? y : x;
    }

    internal void SetFrom(LexicalSortKey other) {
        _treeOrdinal = other._treeOrdinal;
        Volatile.Write(ref _position, other._position);
    }
}
