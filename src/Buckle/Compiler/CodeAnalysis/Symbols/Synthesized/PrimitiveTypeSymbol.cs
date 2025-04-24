using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PrimitiveTypeSymbol : NamedTypeSymbol {
    internal PrimitiveTypeSymbol(string name, SpecialType specialType, int arity = 0) {
        this.name = name;
        this.specialType = specialType;
        this.arity = arity;
        templateParameters = ConstructTemplateParameters();
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override SpecialType specialType { get; }

    public override TypeKind typeKind => TypeKind.Primitive;

    public override int arity { get; }

    internal override bool mangleName => false;

    internal override Compilation declaringCompilation => null;

    internal override Symbol containingSymbol => null;

    internal override NamedTypeSymbol constructedFrom => this;

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override bool isAbstract => false;

    internal override bool isStatic => false;

    internal override bool isSealed => false;

    internal override bool isRefLikeType => false;

    internal override NamedTypeSymbol baseType => null;

    internal override SyntaxReference syntaxReference => throw new InvalidOperationException();

    internal override TextLocation location => null;

    internal override IEnumerable<string> memberNames => throw new InvalidOperationException();

    internal override bool isImplicitlyDeclared => true;

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        throw new InvalidOperationException();
    }

    private ImmutableArray<TemplateParameterSymbol> ConstructTemplateParameters() {
        if (arity == 0)
            return [];

        var builder = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        var typeType = new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Type));

        for (var i = 0; i < arity; i++)
            builder.Add(new SynthesizedTemplateParameterSymbol(this, typeType, i));

        return builder.ToImmutableAndFree();
    }
}
