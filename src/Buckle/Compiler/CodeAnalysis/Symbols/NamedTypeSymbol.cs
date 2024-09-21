using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, ITypeSymbolWithMembers, ISymbolWithTemplates {
    internal NamedTypeSymbol(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<Symbol> symbols,
        TypeDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(declaration?.identifier?.text, accessibility) {
        members = symbols;
        this.declaration = declaration;
        this.templateParameters = templateParameters;
        this.templateConstraints = templateConstraints;
        this.modifiers = modifiers;

        foreach (var member in members)
            member.SetContainingType(this);
    }

    public override SymbolKind kind => SymbolKind.Type;

    public override bool isStatic => (modifiers & DeclarationModifiers.Static) != 0;

    public override bool isAbstract => (modifiers & DeclarationModifiers.Abstract) != 0;

    public override bool isSealed => (modifiers & DeclarationModifiers.Sealed) != 0;

    public override bool isVirtual => false;

    public override bool isOverride => false;

    public ImmutableArray<MethodSymbol> constructors => GetConstructors();

    public ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public abstract TemplateMap templateSubstitution { get; }

    internal abstract ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments { get; }

    internal override int arity => templateParameters.Length;

    internal bool isLowLevel => (modifiers & DeclarationModifiers.LowLevel) != 0;

    internal DeclarationModifiers modifiers;

    internal TypeDeclarationSyntax declaration { get; }

    /// <summary>
    /// Gets a string representation of the type signature without template parameter names.
    /// </summary>
    public string Signature() {
        var signature = new StringBuilder($"{name}<");
        var isFirst = true;

        foreach (var parameter in templateParameters) {
            if (isFirst)
                isFirst = false;
            else
                signature.Append(", ");

            signature.Append(parameter);
        }

        signature.Append('>');

        return signature.ToString();
    }

    private ImmutableArray<MethodSymbol> GetConstructors() {
        var candidates = GetMembers(WellKnownMemberNames.InstanceConstructorName);

        if (candidates.IsEmpty)
            return [];

        var constructors = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var candidate in candidates) {
            if (candidate is MethodSymbol method && candidate.containingType == this)
                constructors.Add(method);
        }

        return constructors.ToImmutableAndFree();
    }
}
