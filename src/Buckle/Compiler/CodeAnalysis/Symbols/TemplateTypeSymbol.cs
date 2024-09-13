using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type wrapper of a template parameter replaced after the template is created.
/// </summary>
internal sealed class TemplateTypeSymbol : NamedTypeSymbol {
    internal TemplateTypeSymbol(ParameterSymbol template) : base(
            [],
            [],
            [],
            LibraryUtilities.CreateDeclaration(template.name),
            DeclarationModifiers.None,
            Accessibility.Public
        ) {
        this.template = template;
    }

    internal ParameterSymbol template { get; }

    internal BoundType baseType { get; private set; }

    public override bool isStatic => false;

    public override bool isAbstract => false;

    public override bool isSealed => false;

    internal void AddBaseType(BoundType baseType) {
        this.baseType = baseType;
        _lazyMembers = null;
    }

    protected override void ConstructLazyMembers() {
        _lazyMembers = new List<Symbol>();
        NamedTypeSymbol current = this;

        do {
            _lazyMembers.AddRange(current.members);

            if (current is TemplateTypeSymbol t)
                current = t.baseType?.typeSymbol as NamedTypeSymbol;
            else if (current is ClassSymbol c)
                current = c.baseType?.typeSymbol as ClassSymbol;
            else
                current = null;
        } while (current is not null);
    }
}
