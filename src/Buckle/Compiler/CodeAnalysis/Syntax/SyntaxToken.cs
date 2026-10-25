using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a token in the syntax tree.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public sealed class SyntaxToken {
    /// <summary>
    /// A predicate function that checks if the passed <see cref="SyntaxToken" /> has a width greater than zero.
    /// </summary>
    internal static readonly Func<SyntaxToken, bool> NonZeroWidth = t => t.width > 0;

    /// <summary>
    /// A predicate function that always returns true. Used as a default predicate function.
    /// </summary>
    internal static readonly Func<SyntaxToken, bool> Any = t => true;

    /// <summary>
    /// Creates a new <see cref="SyntaxToken" /> with a parent, underlying token, and an absolute position.
    /// </summary>
    internal SyntaxToken(SyntaxNode parent, GreenNode token, int position, int index) {
        this.parent = parent;
        node = token;
        this.position = position;
        this.index = index;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxToken"/> from an underlying token.
    /// </summary>
    internal SyntaxToken(GreenNode token) {
        node = token;
    }

    /// <summary>
    /// If the token has been fabricated by the compiler.
    /// </summary>
    public bool isFabricated => node?.isFabricated ?? false;

    /// <summary>
    /// The parent of this token.
    /// </summary>
    public SyntaxNode parent { get; }

    /// <summary>
    /// The underlying token node.
    /// </summary>
    internal GreenNode node { get; }

    /// <summary>
    /// The absolute start position of this token in relation to a <see cref="SourceText" />.
    /// </summary>
    public int position { get; }

    internal int endPosition => node is not null ? position + node.fullWidth : 0;

    /// <summary>
    /// The slot index of this token in relation to the parent.
    /// </summary>
    public int index { get; }

    /// <summary>
    /// The kind of token.
    /// </summary>
    public SyntaxKind kind => node.kind;

    /// <summary>
    /// The value of the token, if any value exists.
    /// </summary>
    public object value => node.GetValue();

    /// <summary>
    /// The string value of the token, if any string value exists.
    /// </summary>
    public string valueText => node.GetValueText();

    /// <summary>
    /// The text of the token, if any text exists.
    /// </summary>
    public string text => ToString();

    /// <summary>
    /// The width of the token, excluding any trivia.
    /// </summary>
    public int width => node?.width ?? 0;

    /// <summary>
    /// The full width of the token, including any trivia.
    /// </summary>
    public int fullWidth => node?.fullWidth ?? 0;

    /// <summary>
    /// The span of the token, excluding any trivia.
    /// </summary>
    public TextSpan span => node is not null ? new TextSpan(position + node.GetLeadingTriviaWidth(), node.width) : null;

    /// <summary>
    /// The full span of the token, including any trivia.
    /// </summary>
    /// <returns></returns>
    public TextSpan fullSpan => new TextSpan(position, fullWidth);

    /// <summary>
    /// The <see cref="SyntaxTree" /> that contains this token.
    /// </summary>
    public SyntaxTree syntaxTree => parent?.syntaxTree;

    /// <summary>
    /// The absolute location of this token in the
    /// <see cref="SyntaxTree.text" /> of the <see cref="SyntaxTree" /> that contains this token.
    /// </summary>
    public TextLocation location {
        get {
            if (syntaxTree is null)
                return null;

            var primaryLocation = new TextLocation(syntaxTree.text, span, syntaxTree);

            if (node.hasAnnotationsDirectly) {
                var locationAnnotations = GetAnnotations("Location");

                if (locationAnnotations.Any()) {
                    var annotation = locationAnnotations.Single();
                    return new CompoundTextLocation(primaryLocation, (SyntaxNode)annotation.data);
                }
            }

            return primaryLocation;
        }
    }

    /// <summary>
    /// Determines whether this token has any leading trivia.
    /// </summary>
    public bool hasLeadingTrivia => leadingTrivia.Count > 0;

    /// <summary>
    /// Determines whether this token has any trailing trivia.
    /// </summary>
    public bool hasTrailingTrivia => trailingTrivia.Count > 0;

    /// <summary>
    /// The leading trivia of this token, if any.
    /// </summary>
    public SyntaxTriviaList leadingTrivia => node is not null ?
        new SyntaxTriviaList(this, node.GetLeadingTrivia(), position)
        : null;

    /// <summary>
    /// The trailing trivia of this token, if any.
    /// </summary>
    public SyntaxTriviaList trailingTrivia {
        get {
            if (node is null)
                return null;

            var leading = node.GetLeadingTrivia();
            var index = 0;

            if (leading is not null)
                index = leading.isList ? leading.slotCount : 1;

            var trailingGreen = node.GetTrailingTrivia();
            var trailingPosition = position + fullWidth;

            if (trailingGreen is not null)
                trailingPosition -= trailingGreen.fullWidth;

            return new SyntaxTriviaList(this, trailingGreen, trailingPosition, index);
        }
    }

    internal SyntaxTree GetPrimarySyntaxTree() {
        if (location is CompoundTextLocation compound)
            return compound.secondaryLocation.tree;

        return syntaxTree;
    }

    public SyntaxToken GetNextToken(bool includeZeroWidth = false, bool includeSkipped = false) {
        if (node is null)
            return default;

        return SyntaxNavigator.Instance.GetNextToken(this, includeZeroWidth, includeSkipped);
    }

    public SyntaxToken GetPreviousToken(
        bool includeZeroWidth = false,
        bool includeSkipped = false,
        bool includeDirectives = false) {
        if (node is null)
            return default;

        return SyntaxNavigator.Instance.GetPreviousToken(this, includeZeroWidth, includeSkipped, includeDirectives);
    }

    internal InternalSyntax.DirectiveStack ApplyDirectives(InternalSyntax.DirectiveStack stack) {
        return ((InternalSyntax.BelteSyntaxNode)node).ApplyDirectives(stack);
    }

    internal IEnumerable<SyntaxAnnotation> GetAnnotations(string annotationKind) {
        return node?.GetAnnotations(annotationKind) ?? SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();
    }

    internal SyntaxToken WithAdditionalAnnotations(params SyntaxAnnotation[] annotations) {
        return WithAdditionalAnnotations((IEnumerable<SyntaxAnnotation>)annotations);
    }

    internal SyntaxToken WithAdditionalAnnotations(IEnumerable<SyntaxAnnotation> annotations) {
        ArgumentNullException.ThrowIfNull(annotations);

        if (node is not null) {
            return new SyntaxToken(
                parent: null,
                token: node.WithAdditionalAnnotationsGreen(annotations),
                position: 0,
                index: 0
            );
        }

        return default;
    }

    internal SyntaxToken WithoutAnnotations(string annotationKind) {
        ArgumentNullException.ThrowIfNull(annotationKind);

        if (HasAnnotations(annotationKind))
            return WithoutAnnotations(GetAnnotations(annotationKind));

        return this;
    }

    internal SyntaxToken WithoutAnnotations(IEnumerable<SyntaxAnnotation> annotations) {
        ArgumentNullException.ThrowIfNull(annotations);

        if (node is not null) {
            return new SyntaxToken(
                parent: null,
                token: node.WithoutAnnotationsGreen(annotations),
                position: 0,
                index: 0
            );
        }

        return default;
    }

    internal bool HasAnnotations(string annotationKind) {
        return node?.HasAnnotations(annotationKind) ?? false;
    }

    public override string ToString() {
        return node is not null ? node.ToString() : "";
    }

    public void WriteTo(TextWriter writer) {
        node?.WriteTo(writer);
    }

    public static bool operator ==(SyntaxToken left, SyntaxToken right) {
        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(SyntaxToken left, SyntaxToken right) {
        return !left.Equals(right);
    }

    public bool Equals(SyntaxToken other) {
        if (other is null)
            return false;

        return parent == other.parent &&
               node == other.node &&
               position == other.position &&
               index == other.index;
    }

    public override bool Equals(object obj) {
        return obj is SyntaxToken token && Equals(token);
    }

    public override int GetHashCode() {
        return Hash.Combine(parent, Hash.Combine(node, Hash.Combine(position, index)));
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + (node is not null ? node.kind : "None") + " " + ToString();
    }

    internal SyntaxToken WithLeadingTrivia(SyntaxTriviaList trivia) {
        return WithLeadingTrivia((IEnumerable<SyntaxTrivia>)trivia);
    }

    internal SyntaxToken WithLeadingTrivia(IEnumerable<SyntaxTrivia> trivia) {
        return node is not null
            ? new SyntaxToken(
                null,
                node.WithLeadingTrivia(GreenNode.CreateList(trivia, static t => t.green)),
                position: 0,
                index: 0
              )
            : default;
    }

    internal SyntaxToken WithTrailingTrivia(SyntaxTriviaList trivia) {
        return WithTrailingTrivia((IEnumerable<SyntaxTrivia>)trivia);
    }

    internal SyntaxToken WithTrailingTrivia(IEnumerable<SyntaxTrivia>? trivia) {
        return node is not null
            ? new SyntaxToken(
                null,
                node.WithTrailingTrivia(GreenNode.CreateList(trivia, static t => t.green)),
                position: 0,
                index: 0
              )
            : default;
    }

    internal bool IsPartOfStructuredTrivia() {
        return parent?.IsPartOfStructuredTrivia() ?? false;
    }
}
