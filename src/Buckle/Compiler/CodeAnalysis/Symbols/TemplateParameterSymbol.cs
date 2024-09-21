
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type wrapper of a template parameter replaced after the template is created.
/// </summary>
internal sealed class TemplateParameterSymbol : TypeSymbol {
    internal TemplateParameterSymbol(ParameterSymbol parameter, NamedTypeSymbol baseType) : base(parameter.name) {
        this.parameter = parameter;
        this.baseType = baseType;
    }

    public override bool isStatic => false;

    public override bool isAbstract => false;

    public override bool isSealed => false;

    public override bool isVirtual => false;

    public override bool isOverride => false;

    internal ParameterSymbol parameter { get; }

    internal override NamedTypeSymbol baseType { get; }

    internal override TypeKind typeKind => TypeKind.TemplateParameter;

    protected override void ConstructLazyMembers() {
        _lazyMembers = [.. baseType.GetMembers()];
    }
}
