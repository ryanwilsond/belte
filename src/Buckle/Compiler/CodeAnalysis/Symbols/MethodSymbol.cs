using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A method symbol.
/// </summary>
internal abstract class MethodSymbol : Symbol, ISymbolWithTemplates {
    private protected MethodSymbol() { }

    public override SymbolKind kind => SymbolKind.Method;

    public abstract ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public abstract ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public abstract TemplateMap templateSubstitution { get; }

    internal new virtual MethodSymbol originalDefinition => this;

    internal new bool isDefinition => (object)this == originalDefinition;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;

    internal abstract RefKind refKind { get; }

    internal bool returnsByRef => refKind == RefKind.Ref;

    internal bool returnsByRefConst => refKind == RefKind.RefConst;

    internal abstract bool returnsVoid { get; }

    internal abstract TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal TypeSymbol returnType => returnTypeWithAnnotations.type;

    internal abstract MethodKind methodKind { get; }

    internal virtual bool requiresInstanceReceiver => !isStatic;

    internal abstract int arity { get; }

    internal virtual int parameterCount => parameters.Length;

    internal virtual MethodSymbol constructedFrom => this;

    internal abstract ImmutableArray<ParameterSymbol> parameters { get; }

    // TODO finish this

    /// <summary>
    /// Declaration of method (see <see cref="BaseMethodDeclarationSyntax">).
    /// </summary>
    internal BaseMethodDeclarationSyntax declaration { get; }

    internal ImmutableArray<TypeOrConstant> GetTemplateParametersAsTemplateArguments() {
        return TemplateMap.TemplateParametersAsTypeOrConstants(templateParameters);
    }

    public override int GetHashCode() {
        unchecked {
            var hash = 17;

            hash = hash * 23 + templateParameters.GetHashCode();
            hash = hash * 23 + templateConstraints.GetHashCode();
            hash = hash * 23 + modifiers.GetHashCode();
            hash = hash * 23 + parameters.GetHashCode();
            hash = hash * 23 + type.GetHashCode();
            hash = hash * 23 + name.GetHashCode();
            hash = hash * 23 + declaredAccessibility.GetHashCode();
            hash = hash * 23 + (originalDefinition is null ? 0 : originalDefinition.GetHashCode());
            hash = hash * 23 + (declaration is null ? 0 : declaration.GetHashCode());

            return hash;
        }
    }
}
