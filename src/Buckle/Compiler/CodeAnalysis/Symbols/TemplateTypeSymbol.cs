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

    public override bool isStatic => false;

    internal ParameterSymbol template { get; }

    public override bool isAbstract => false;

    public override bool isSealed => false;
}
