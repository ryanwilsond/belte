using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a field initializer or a global statement.
/// </summary>
internal readonly struct FieldInitializer {
    internal readonly FieldSymbol field;
    internal readonly SyntaxReference syntax;

    internal FieldInitializer(FieldSymbol field, SyntaxNode syntax) {
        this.field = field;
        this.syntax = new SyntaxReference(syntax);
    }
}
