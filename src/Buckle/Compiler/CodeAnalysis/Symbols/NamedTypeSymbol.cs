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
        ImmutableArray<Symbol> members,
        TypeDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(declaration?.identifier?.text, accessibility) {
        this.members = members;
        this.declaration = declaration;
        this.templateParameters = templateParameters;
        this.templateConstraints = templateConstraints;
        this.modifiers = modifiers;

        foreach (var member in members)
            member.SetContainingType(this);
    }

    public override SymbolKind kind => SymbolKind.Type;

    public ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public abstract TemplateMap templateSubstitution { get; }

    internal override bool isStatic => (modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isAbstract => (modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isSealed => (modifiers & DeclarationModifiers.Sealed) != 0;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal ImmutableArray<MethodSymbol> constructors => GetConstructors();

    internal TypeWithAnnotations typeWithAnnotations { get; private set; }

    internal abstract ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments { get; }

    internal override int arity => templateParameters.Length;

    internal bool isLowLevel => (modifiers & DeclarationModifiers.LowLevel) != 0;

    internal DeclarationModifiers modifiers;

    internal TypeDeclarationSyntax declaration { get; }

    internal override ImmutableArray<Symbol> members { get; }

    internal new NamedTypeSymbol originalDefinition => this;

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

    internal override void AddAnnotations(TypeWithAnnotations annotations) {
        typeWithAnnotations = annotations;
    }

    internal override bool InheritsFrom(TypeSymbol other) {
        if (other is null)
            return false;

        if (this == other)
            return true;

        if (typeKind != other.typeKind)
            return false;

        return InheritsFrom(other.baseType);
    }

    internal override int GetInheritanceDepth(TypeSymbol other) {
        if (!InheritsFrom(other))
            return -1;

        var depth = 0;
        var current = this;

        while (current != other) {
            depth++;
            current = current.baseType;
        }

        return depth;
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
