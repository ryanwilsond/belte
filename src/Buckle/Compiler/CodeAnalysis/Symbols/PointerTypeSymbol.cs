using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PointerTypeSymbol : TypeSymbol {
    internal PointerTypeSymbol(TypeWithAnnotations pointedAtType) {
        pointedAtTypeWithAnnotations = pointedAtType;
    }

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isStatic => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    public override SpecialType specialType => SpecialType.Pointer;

    internal TypeWithAnnotations pointedAtTypeWithAnnotations { get; }

    internal TypeSymbol pointedAtType => pointedAtTypeWithAnnotations.type;

    internal override NamedTypeSymbol baseType => null;

    internal override bool isRefLikeType => false;

    public override SymbolKind kind => SymbolKind.PointerType;

    public override TypeKind typeKind => TypeKind.Pointer;

    internal override Symbol containingSymbol => null;

    internal override TextLocation location => null;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override SyntaxReference syntaxReference => null;

    public override bool isObjectType => false;

    public override bool isPrimitiveType => false;

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

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name, int arity) {
        return [];
    }

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitPointerType(this);
    }

    internal override TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument a) {
        return visitor.VisitPointerType(this, a);
    }

    public override int GetHashCode() {
        var indirections = 0;
        TypeSymbol current = this;

        while (current.typeKind == TypeKind.Pointer) {
            indirections += 1;
            current = ((PointerTypeSymbol)current).pointedAtType;
        }

        return Hash.Combine(current, indirections);
    }

    internal override bool ApplyNullableTransforms(
        byte defaultTransformFlag,
        ImmutableArray<byte> transforms,
        ref int position,
        out TypeSymbol result) {
        var oldPointedAtType = pointedAtTypeWithAnnotations;

        if (!oldPointedAtType.ApplyNullableTransforms(defaultTransformFlag, transforms, ref position, out var newPointedAtType)) {
            result = this;
            return false;
        }

        result = WithPointedAtType(newPointedAtType);
        return true;
    }

    internal PointerTypeSymbol WithPointedAtType(TypeWithAnnotations newPointedAtType) {
        return pointedAtTypeWithAnnotations.IsSameAs(newPointedAtType) ? this : new PointerTypeSymbol(newPointedAtType);
    }

    internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
        return Equals(t2 as PointerTypeSymbol, comparison);
    }

    private bool Equals(PointerTypeSymbol other, TypeCompareKind comparison) {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null || !other.pointedAtTypeWithAnnotations.Equals(pointedAtTypeWithAnnotations, comparison))
            return false;

        return true;
    }
}
