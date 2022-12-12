using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A type clause, includes array dimensions, type name, and attributes.
/// </summary>
internal sealed class TypeClause : Node {
    /// <param name="attributes">Simple flag modifiers on a type (e.g. [NotNull])</param>
    /// <param name="constRefKeyword">Const keyword referring to a constant reference type</param>
    /// <param name="refKeyword">Ref keyword referring to a reference type</param>
    /// <param name="constKeyword">Const keyword referring to a constant type</param>
    /// <param name="brackets">Brackets, determine array dimensions</param>
    internal TypeClause(SyntaxTree syntaxTree, ImmutableArray<(Token, Token, Token)> attributes,
        Token constRefKeyword, Token refKeyword, Token constKeyword, Token typeName,
        ImmutableArray<(Token, Token)> brackets) : base(syntaxTree) {
        this.attributes = attributes;
        this.constRefKeyword = constRefKeyword;
        this.refKeyword = refKeyword;
        this.constKeyword = constKeyword;
        this.typeName = typeName;
        this.brackets = brackets;
    }

    /// <summary>
    /// Simple flag modifiers on a type.
    /// </summary>
    /// <param name="openBracket">Open square bracket token</param>
    /// <param name="identifier">Name of the attribute</param>
    /// <param name="closeBracket">Close square bracket token</param>
    internal ImmutableArray<(Token openBracket, Token identifier, Token closeBracket)> attributes { get; }

    /// <summary>
    /// Const keyword referring to a constant reference type, only valid if the refKeyword field is also set.
    /// </summary>
    internal Token? constRefKeyword { get; }

    /// <summary>
    /// Ref keyword referring to a reference type.
    /// </summary>
    internal Token? refKeyword { get; }

    /// <summary>
    /// Const keyword referring to a constant type.
    /// </summary>
    internal Token? constKeyword { get; }

    internal Token typeName { get; }

    /// <summary>
    /// Brackets defining array dimensions ([] -> 1 dimension, [][] -> 2 dimensions).
    /// </summary>
    /// <param name="openBracket">Open square bracket token</param>
    /// <param name="closeBracket">Close square bracket token</param>
    internal ImmutableArray<(Token openBracket, Token closeBracket)> brackets { get; }

    internal override SyntaxType type => SyntaxType.TypeClause;

    internal override IEnumerable<Node> GetChildren() {
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

        yield return typeName;

        foreach (var pair in brackets) {
            yield return pair.openBracket;
            yield return pair.closeBracket;
        }
    }
}
