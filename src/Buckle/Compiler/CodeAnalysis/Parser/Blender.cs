using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Blends nodes from an old and new tree, following the given changes.
/// </summary>
internal readonly partial struct Blender {
    private readonly Lexer _lexer;
    private readonly Cursor _oldTreeCursor;
    private readonly ImmutableStack<TextChangeRange> _changes;
    private readonly int _newPosition;
    private readonly int _changeDelta;
    private readonly DirectiveStack _newDirectives;
    private readonly DirectiveStack _oldDirectives;
    private readonly LexerMode _newLexerDrivenMode;

    /// <summary>
    /// Creates an instance of <see cref="Blender" />.
    /// </summary>
    internal Blender(Lexer lexer, SyntaxNode oldTree, IEnumerable<TextChangeRange> changes) {
        _lexer = lexer;
        _changes = ImmutableStack.Create<TextChangeRange>();

        if (changes is not null) {
            var collapsed = TextChangeRange.Collapse(changes);
            var affectedRange = ExtendToAffectedRange(oldTree, collapsed);
            _changes = _changes.Push(affectedRange);
        }

        if (oldTree is null) {
            _oldTreeCursor = new Cursor();
            _newPosition = _lexer.position;
        } else {
            _oldTreeCursor = Cursor.FromRoot(oldTree).MoveToFirstChild();
            _newPosition = 0;
        }

        _changeDelta = 0;
        _oldDirectives = default;
        _newDirectives = default;
        _newLexerDrivenMode = 0;
    }

    private Blender(
        Lexer lexer,
        Cursor oldTreeCursor,
        ImmutableStack<TextChangeRange> changes,
        int newPosition,
        int changeDelta,
        DirectiveStack newDirectives,
        DirectiveStack oldDirectives,
        LexerMode newLexerDrivenMode) {
        _lexer = lexer;
        _oldTreeCursor = oldTreeCursor;
        _changes = changes;
        _newPosition = newPosition;
        _changeDelta = changeDelta;
        _newDirectives = newDirectives;
        _oldDirectives = oldDirectives;
        _newLexerDrivenMode = 0;
    }

    /// <summary>
    /// Gets the next node.
    /// </summary>
    internal BlendedNode ReadNode(LexerMode mode) {
        return ReadNodeOrToken(mode, asToken: false);
    }

    /// <summary>
    /// Gets the next token.
    /// </summary>
    internal BlendedNode ReadToken(LexerMode mode) {
        return ReadNodeOrToken(mode, asToken: true);
    }

    private BlendedNode ReadNodeOrToken(LexerMode mode, bool asToken) {
        var reader = new Reader(this);
        return reader.ReadNodeOrToken(mode, asToken);
    }

    private static TextChangeRange ExtendToAffectedRange(SyntaxNode oldTree, TextChangeRange changeRange) {
        // Increase if needed
        const int MaxLookahead = 1;

        var lastCharIndex = oldTree.fullSpan.length - 1;
        var start = Math.Max(Math.Min(changeRange.span.start, lastCharIndex), 0);

        for (var i = 0; start > 0 && i <= MaxLookahead;) {
            var token = oldTree.FindToken(start);
            start = Math.Max(0, token.position - 1);

            if (token.fullWidth > 0)
                i++;
        }

        // TODO Interpolation handling?

        var finalSpan = TextSpan.FromBounds(start, changeRange.span.end);
        var finalLength = changeRange.newLength + (changeRange.span.start - start);

        return new TextChangeRange(finalSpan, finalLength);
    }
}
