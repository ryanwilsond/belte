using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A type 'expression', includes array dimensions, type name, and attributes.
/// </summary>
internal sealed class TypeSyntax : SyntaxNode {
    /// <param name="attributes">Simple flag modifiers on a type (e.g. [NotNull]).</param>
    /// <param name="constRefKeyword">Const keyword referring to a constant reference type.</param>
    /// <param name="refKeyword">Ref keyword referring to a reference type.</param>
    /// <param name="constKeyword">Const keyword referring to a constant type.</param>
    /// <param name="varKeyword">Var keyword referring to a variable type.</param>
    /// <param name="typeName">Name referring to a type.</param>
    /// <param name="brackets">Brackets, determine array dimensions.</param>
    internal TypeSyntax(SyntaxTree syntaxTree, SyntaxList<AttributeSyntax> attributes,
        SyntaxToken constRefKeyword, SyntaxToken refKeyword, SyntaxToken constKeyword, SyntaxToken varKeyword,
        SyntaxToken typeName, ImmutableArray<(SyntaxToken, SyntaxToken)> brackets) : base(syntaxTree) {
        this.attributes = attributes;
        this.constRefKeyword = constRefKeyword;
        this.refKeyword = refKeyword;
        this.constKeyword = constKeyword;
        this.varKeyword = varKeyword;
        this.typeName = typeName;
        this.brackets = brackets;
    }

    /// <summary>
    /// Simple flag modifiers on a type.
    /// </summary>
    internal SyntaxList<AttributeSyntax> attributes { get; }

    /// <summary>
    /// Const keyword referring to a constant reference type, only valid if the
    /// <see cref="TypeSyntax.refKeyword" /> field is also set.
    /// </summary>
    internal SyntaxToken? constRefKeyword { get; }

    /// <summary>
    /// Ref keyword referring to a reference type.
    /// </summary>
    internal SyntaxToken? refKeyword { get; }

    /// <summary>
    /// Const keyword referring to a constant type.
    /// </summary>
    internal SyntaxToken? constKeyword { get; }

    /// <summary>
    /// Var keyword referring to a variable type.
    /// </summary>
    internal SyntaxToken? varKeyword { get; }

    /// <summary>
    /// Name referring to a type.
    /// </summary>
    internal SyntaxToken? typeName { get; }

    /// <summary>
    /// Brackets defining array dimensions ([] -> 1 dimension, [][] -> 2 dimensions).
    /// </summary>
    /// <param name="openBracket">Open square bracket token.</param>
    /// <param name="closeBracket">Close square bracket token.</param>
    internal ImmutableArray<(SyntaxToken openBracket, SyntaxToken closeBracket)> brackets { get; }

    internal override SyntaxKind kind => SyntaxKind.Type;

    internal override IEnumerable<SyntaxNode> GetChildren() {
        foreach (var attribute in attributes) {
            yield return attribute.openBracket;
            yield return attribute.identifier;
            yield return attribute.closeBracket;
        }

        if (constRefKeyword != null && constRefKeyword.fullSpan != null)
            yield return constRefKeyword;

        if (refKeyword != null && refKeyword.fullSpan != null)
            yield return refKeyword;

        if (constKeyword != null && constKeyword.fullSpan != null)
            yield return constKeyword;

        if (varKeyword != null && varKeyword.fullSpan != null)
            yield return varKeyword;

        if (typeName != null && typeName.fullSpan != null)
            yield return typeName;

        foreach (var pair in brackets) {
            yield return pair.openBracket;
            yield return pair.closeBracket;
        }
    }
}
