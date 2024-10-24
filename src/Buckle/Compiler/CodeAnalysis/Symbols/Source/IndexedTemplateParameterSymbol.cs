using System;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class IndexedTemplateParameterSymbol : TemplateParameterSymbol {
    private static TemplateParameterSymbol[] ParameterPool = [];

    private readonly int _index;

    private IndexedTemplateParameterSymbol(int index) {
        _index = index;
    }

    internal override TemplateParameterKind templateParameterKind => TemplateParameterKind.Method;

    internal override bool isImplicitlyDeclared => true;

    internal override int ordinal => _index;

    internal override bool hasPrimitiveTypeConstraint => false;

    internal override bool isPrimitiveTypeFromConstraintTypes => false;

    internal override bool hasObjectTypeConstraint => false;

    internal override bool isObjectTypeFromConstraintTypes => false;

    internal override Symbol containingSymbol => null;

    internal override SyntaxReference syntaxReference => null;

    internal override TypeWithAnnotations underlyingType => null;

    internal override TypeOrConstant defaultValue => null;

    internal static TemplateParameterSymbol GetTemplateParameter(int index) {
        if (index >= ParameterPool.Length)
            GrowPool(index + 1);

        return ParameterPool[index];
    }

    internal static ImmutableArray<TemplateParameterSymbol> TakeSymbols(int count) {
        if (count > ParameterPool.Length)
            GrowPool(count);

        var builder = ArrayBuilder<TemplateParameterSymbol>.GetInstance();

        for (var i = 0; i < count; i++)
            builder.Add(GetTemplateParameter(i));

        return builder.ToImmutableAndFree();
    }

    internal override void EnsureConstraintsAreResolved() { }

    internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(
        ConsList<TemplateParameterSymbol> inProgress) {
        return [];
    }

    internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
        return null;
    }

    internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
        return null;
    }

    internal static ImmutableArray<TypeOrConstant> Take(int count) {
        if (count > ParameterPool.Length)
            GrowPool(count);

        var builder = ArrayBuilder<TypeOrConstant>.GetInstance();

        for (var i = 0; i < count; i++)
            builder.Add(new TypeOrConstant(GetTemplateParameter(i)));

        return builder.ToImmutableAndFree();
    }

    private static void GrowPool(int count) {
        var initialPool = ParameterPool;

        while (count > initialPool.Length) {
            var newPoolSize = (count + 0x0F) & ~0xF;
            var newPool = new TemplateParameterSymbol[newPoolSize];

            Array.Copy(initialPool, newPool, initialPool.Length);

            for (var i = initialPool.Length; i < newPool.Length; i++)
                newPool[i] = new IndexedTemplateParameterSymbol(i);

            Interlocked.CompareExchange(ref ParameterPool, newPool, initialPool);
            initialPool = ParameterPool;
        }
    }

    internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
        return ReferenceEquals(this, t2);
    }

    public override int GetHashCode() {
        return _index;
    }
}
