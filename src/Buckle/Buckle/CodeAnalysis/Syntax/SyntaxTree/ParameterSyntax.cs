
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter for a method or function declaration.
/// </summary>
internal sealed partial class ParameterSyntax : SyntaxNode {
    /// <param name="type"><see cref="TypeSyntax" /> of the parameter.</param>
    /// <param name="identifier">Name of the parameter.</param>
    /// <param name="equals">
    /// Optional; used to separate the name from the default expression, always and only used if
    /// <param name="defaultValue" /> is specified.
    /// </param>
    /// <param name="defaultValue"/>
    /// Optional; default value of a parameter if no corresponding argument is used when calling the parent method
    /// or function.
    /// Must be computable at compile-time.
    /// </param>
    internal ParameterSyntax(
        SyntaxTree syntaxTree, TypeSyntax type, SyntaxToken identifier,
        SyntaxToken equals, ExpressionSyntax defaultValue)
        : base(syntaxTree) {
        this.type = type;
        this.identifier = identifier;
        this.equals = equals;
        this.defaultValue = defaultValue;
    }

    /// <summary>
    /// <see cref="TypeSyntax" /> of the parameter.
    /// </summary>
    internal TypeSyntax type { get; }

    /// <summary>
    /// Name of the parameter.
    /// </summary>
    internal SyntaxToken identifier { get; }

    /// <summary>
    ///Optional; used to separate the name from the default expression, always and only used if
    /// <param name="defaultValue" /> is specified.
    /// </summary>
    internal SyntaxToken? equals { get; }

    /// <summary>
    /// Optional; default value of a parameter if no corresponding argument is used when calling the parent function
    /// or method.
    /// Must be computable at compile-time.
    /// </summary>
    internal ExpressionSyntax? defaultValue { get; }

    internal override SyntaxKind kind => SyntaxKind.Parameter;
}

internal sealed partial class SyntaxFactory {
    internal ParameterSyntax Parameter(
        TypeSyntax type, SyntaxToken identifier, SyntaxToken equals, ExpressionSyntax defaultValue) =>
        Create(new ParameterSyntax(_syntaxTree, type, identifier, equals, defaultValue));
}
