using System.Collections.Generic;
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
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<Symbol> symbols,
        ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments,
        ClassDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility,
        BoundType baseType)
        : base(templateParameters, templateConstraints, symbols, declaration, modifiers, accessibility) {
        this.defaultFieldAssignments = defaultFieldAssignments;
        this.baseType = baseType;
    }

    /// <summary>
    /// Statements that assigns fields with specified initializers. Used in constructors.
    /// </summary>
    internal ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments { get; private set; }

    /// <summary>
    /// The type this symbol inherits from; Object if not explicitly specified.
    /// </summary>
    internal BoundType baseType { get; private set; }

    internal void UpdateInternals(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<Symbol> symbols,
        ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments,
        BoundType baseType) {
        UpdateInternals(templateParameters, templateConstraints, symbols);
        this.defaultFieldAssignments = defaultFieldAssignments;
        this.baseType = baseType;
    }

    protected override void ConstructLazyMembers() {
        _lazyMembers = new List<Symbol>();
        var current = this;

        do {
            _lazyMembers.AddRange(current.members);
            current = current.baseType?.typeSymbol as ClassSymbol;
        } while (current is not null);
    }
}
