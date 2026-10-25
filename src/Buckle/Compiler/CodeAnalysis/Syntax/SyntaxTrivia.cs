using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public class SyntaxTrivia {
    /// <summary>
    /// Creates a new <see cref="SyntaxTrivia" /> with an existing token, underlying trivia, and position.
    /// Also contains the index of this trivia in relation to other trivias in the trivia list that contains
    /// this trivia.
    /// </summary>
    internal SyntaxTrivia(SyntaxToken token, GreenNode trivia, int position, int index) {
        this.token = token;
        green = trivia;
        this.position = position;
        this.index = index;
    }

    /// <summary>
    /// The token that this trivia is wrapping.
    /// </summary>
    public SyntaxToken token { get; }

    /// <summary>
    /// The underlying trivia.
    /// </summary>
    internal GreenNode green { get; }

    /// <summary>
    /// The index that this trivia is in relation to an owning trivia list.
    /// </summary>
    public int index { get; }

    /// <summary>
    /// The start position of this trivia.
    /// </summary>
    public int position { get; }

    /// <summary>
    /// The width of the trivia. Is the same as <see cref="fullWidth" />.
    /// </summary>
    public int width => green?.width ?? 0;

    /// <summary>
    /// The full width of the trivia. Is the same as <see cref="width" />.
    /// </summary>
    public int fullWidth => green?.width ?? 0;

    /// <summary>
    /// The kind of trivia.
    /// </summary>
    public SyntaxKind kind => green?.kind ?? SyntaxKind.None;

    /// <summary>
    /// The span of the trivia. Should be the same as <see cref="fullSpan" />.
    /// </summary>
    /// <returns></returns>
    public TextSpan span => green is not null
        ? new TextSpan(position + green.GetLeadingTriviaWidth(), green.width)
        : null;

    /// <summary>
    /// The full span of the trivia. Should be the same as <see cref="span" />.
    /// </summary>
    /// <returns></returns>
    public TextSpan fullSpan => green is not null ? new TextSpan(position, green.fullWidth) : null;

    /// <summary>
    /// If the trivia contains any diagnostics.
    /// </summary>
    public bool containsDiagnostics => green?.containsDiagnostics ?? false;

    /// <summary>
    /// The <see cref="SyntaxTree" /> that contains this trivia.
    /// </summary>
    public SyntaxTree syntaxTree => token.syntaxTree;

    /// <summary>
    /// The location of this trivia in the <see cref="SourceText" /> that contains it.
    /// </summary>
    /// <returns></returns>
    public TextLocation location {
        get {
            if (syntaxTree is null)
                return null;

            var primaryLocation = new TextLocation(syntaxTree.text, span, syntaxTree);

            if (green.hasAnnotationsDirectly) {
                var locationAnnotations = GetAnnotations("Location");

                if (locationAnnotations.Any()) {
                    var annotation = locationAnnotations.Single();
                    return new CompoundTextLocation(primaryLocation, (SyntaxNode)annotation.data);
                }
            }

            return primaryLocation;
        }
    }

    internal bool isDirective => green?.isDirective ?? false;

    internal bool hasStructure => green?.isStructuredTrivia ?? false;

    internal SyntaxNode GetStructure() {
        return hasStructure ? green.GetStructure(this) : null;
    }

    internal bool TryGetStructure(out SyntaxNode structure) {
        structure = GetStructure();
        return structure is not null;
    }

    internal bool IsPartOfStructuredTrivia() {
        return token.parent?.IsPartOfStructuredTrivia() ?? false;
    }

    internal IEnumerable<SyntaxAnnotation> GetAnnotations(string annotationKind) {
        return green is not null
            ? green.GetAnnotations(annotationKind)
            : SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();
    }

    internal SyntaxTrivia WithAdditionalAnnotations(params SyntaxAnnotation[] annotations) {
        return WithAdditionalAnnotations((IEnumerable<SyntaxAnnotation>)annotations);
    }

    internal SyntaxTrivia WithAdditionalAnnotations(IEnumerable<SyntaxAnnotation> annotations) {
        ArgumentNullException.ThrowIfNull(annotations);

        if (green is not null) {
            return new SyntaxTrivia(
                token: default,
                trivia: green.WithAdditionalAnnotationsGreen(annotations),
                position: 0,
                index: 0
            );
        }

        return default;
    }

    internal SyntaxTrivia WithoutAnnotations(string annotationKind) {
        ArgumentNullException.ThrowIfNull(annotationKind);

        if (HasAnnotations(annotationKind))
            return WithoutAnnotations(GetAnnotations(annotationKind));

        return this;
    }

    internal SyntaxTrivia WithoutAnnotations(IEnumerable<SyntaxAnnotation> annotations) {
        ArgumentNullException.ThrowIfNull(annotations);

        if (green is not null) {
            return new SyntaxTrivia(
                token: default,
                trivia: green.WithoutAnnotationsGreen(annotations),
                position: 0,
                index: 0
            );
        }

        return default;
    }

    internal bool HasAnnotations(string annotationKind) {
        return green?.HasAnnotations(annotationKind) ?? false;
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + kind + " " + ToString();
    }
}
