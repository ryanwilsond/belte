
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type wrapper of a template parameter replaced after the template is created.
/// </summary>
internal sealed class TemplateParameterSymbol : TypeSymbol {
    internal TemplateParameterSymbol(ParameterSymbol parameter, NamedTypeSymbol baseType) : base(parameter.name) {
        this.parameter = parameter;
        this.baseType = baseType;
    }

    public override SymbolKind kind => SymbolKind.TemplateParameter;

    internal override bool isStatic => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal ParameterSymbol parameter { get; }

    internal override NamedTypeSymbol baseType { get; }

    internal override TypeKind typeKind => TypeKind.TemplateParameter;

    protected override void ConstructLazyMembers() {
        _lazyMembers = [.. baseType.GetMembers()];
    }
}
