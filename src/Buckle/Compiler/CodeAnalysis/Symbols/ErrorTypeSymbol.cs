using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class ErrorTypeSymbol : NamedTypeSymbol {
    internal static readonly ErrorTypeSymbol UnknownResultType = new UnsupportedMetadataTypeSymbol();

    private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;

    public override string name => "";

    public override SymbolKind kind => SymbolKind.ErrorType;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters {
        get {
            if (_lazyTemplateParameters.IsDefault) {
                ImmutableInterlocked.InterlockedCompareExchange(ref _lazyTemplateParameters,
                    GetTemplateParameters(),
                    default
                );
            }

            return _lazyTemplateParameters;
        }
    }

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    internal override TypeKind typeKind => TypeKind.Error;

    internal override Symbol containingSymbol => null;

    internal override SyntaxReference syntaxReference => null;

    internal override NamedTypeSymbol constructedFrom => this;

    internal sealed override Accessibility accessibility => Accessibility.NotApplicable;

    internal sealed override bool isStatic => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isSealed => false;

    internal override NamedTypeSymbol baseType => null;

    internal override int arity => 0;

    internal override bool isObjectType => true;

    internal override bool isPrimitiveType => true;

    internal override IEnumerable<string> memberNames => SpecializedCollections.EmptyEnumerable<string>();

    internal override ImmutableArray<Symbol> GetMembers() {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return [];
    }

    internal override NamedTypeSymbol AsMember(NamedTypeSymbol newOwner) {
        return newOwner.isDefinition ? this : new SubstitutedNestedErrorTypeSymbol(newOwner, this);
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return null;
    }

    private ImmutableArray<TemplateParameterSymbol> GetTemplateParameters() {
        var arity = this.arity;

        if (arity == 0) {
            return [];
        } else {
            var templateParameters = new TemplateParameterSymbol[arity];

            for (var i = 0; i < arity; i++)
                templateParameters[i] = new ErrorTemplateParameterSymbol(this, "", i);

            return templateParameters.AsImmutableOrNull();
        }
    }
}
