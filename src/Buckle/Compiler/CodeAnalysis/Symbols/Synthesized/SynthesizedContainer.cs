using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedContainer : NamedTypeSymbol {
    private readonly ImmutableArray<TemplateParameterSymbol> _templateParameters;
    private readonly ImmutableArray<TemplateParameterSymbol> _constructedFromTemplateParameters;

    private protected SynthesizedContainer(string name, MethodSymbol containingMethod) {
        this.name = name;

        if (containingMethod is null) {
            templateMap = TemplateMap.Empty;
            _templateParameters = [];
        } else {
            templateMap = TemplateMap.Empty.WithConcatAlphaRename(
                containingMethod,
                this,
                out _templateParameters,
                out _constructedFromTemplateParameters
            );
        }
    }

    private protected SynthesizedContainer(
        string name,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        TemplateMap templateMap) {
        this.name = name;
        _templateParameters = templateParameters;
        this.templateMap = templateMap;
    }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => _templateParameters;

    // TODO This should be something
    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    public sealed override string name { get; }

    internal TemplateMap templateMap { get; }

    internal virtual MethodSymbol constructor => null;

    internal ImmutableArray<TemplateParameterSymbol> constructedFromTemplateParameters
        => _constructedFromTemplateParameters;

    internal override TextLocation location => null;

    internal override SyntaxReference syntaxReference => null;

    internal override IEnumerable<string> memberNames => SpecializedCollections.EmptyEnumerable<string>();

    internal override NamedTypeSymbol constructedFrom => this;

    internal override bool isSealed => true;

    internal override bool isAbstract => constructor is null && typeKind != TypeKind.Struct;

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() => [];

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) => [];

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name, int arity) => [];

    internal override Accessibility declaredAccessibility => Accessibility.Private;

    internal override bool isStatic => false;

    internal sealed override bool isRefLikeType => false;

    // TODO Double check these don't have replacements
    // internal sealed override bool isReadOnly => false;

    // internal sealed override bool isConstant => false;

    internal override NamedTypeSymbol baseType => CorLibrary.GetSpecialType(SpecialType.Object);

    public override int arity => templateParameters.Length;

    internal override bool mangleName => arity > 0;

    internal override bool isImplicitlyDeclared => true;

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        Symbol constructor = this.constructor;
        return constructor is null ? [] : [constructor];
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        var constructor = this.constructor;
        return (constructor is not null && name == constructor.name) ? [constructor] : [];
    }

    internal virtual IEnumerable<FieldSymbol> GetFieldsToEmit() {
        foreach (var m in GetMembers()) {
            switch (m.kind) {
                case SymbolKind.Field:
                    yield return (FieldSymbol)m;
                    break;
            }
        }
    }
}
