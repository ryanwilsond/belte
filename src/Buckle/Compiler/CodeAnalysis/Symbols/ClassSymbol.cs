using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A class symbol.
/// </summary>
internal sealed class ClassSymbol : NamedTypeSymbol {
    /// <summary>
    /// Creates a <see cref="ClassSymbol" /> with template parameters and child members.
    /// </summary>
    internal ClassSymbol(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments,
        ClassDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility,
        BoundType baseType)
        : base(templateParameters, symbols, declaration, modifiers, accessibility) {
        this.defaultFieldAssignments = defaultFieldAssignments;
        this.baseType = baseType;
    }

    /// <summary>
    /// Statements that assigns fields with specified initializers. Used in constructors.
    /// </summary>
    internal ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments {
        get; private set;
    }

    /// <summary>
    /// The type this symbol inherits from; Object if not explicitly specified.
    /// </summary>
    internal BoundType baseType { get; }

    internal void UpdateInternals(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments) {
        UpdateInternals(templateParameters, symbols);
        this.defaultFieldAssignments = defaultFieldAssignments;
    }

    public bool Equals(ClassSymbol other) {
        if ((object)this == other)
            return true;

        if (name != other.name)
            return false;
        if (containingType != other.containingType)
            return false;
        if (_declarationModifiers != other._declarationModifiers)
            return false;
        if (templateParameters != other.templateParameters)
            return false;
        if (members != other.members)
            return false;
        if (defaultFieldAssignments != other.defaultFieldAssignments)
            return false;
        if (declaration != other.declaration)
            return false;

        return true;
    }
}
