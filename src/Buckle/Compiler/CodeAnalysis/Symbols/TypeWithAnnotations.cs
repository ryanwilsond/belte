using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Display;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol with null clarification.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal sealed class TypeWithAnnotations {
    internal TypeWithAnnotations(TypeSymbol underlyingType, bool isNullable) {
        type = underlyingType;
        this.isNullable = isNullable;
    }

    internal TypeWithAnnotations(TypeSymbol underlyingType) {
        type = underlyingType;
        isNullable = type.IsNullableType();
    }

    internal TypeSymbol type { get; }

    internal bool isNullable { get; }

    internal bool hasType => type is not null;

    internal TypeKind typeKind => type.typeKind;

    internal SpecialType specialType => type.specialType;

    internal bool isStatic => type.isStatic;

    internal TypeSymbol nullableUnderlyingTypeOrSelf => type.StrippedType();

    internal bool IsSameAs(TypeWithAnnotations other) {
        return ReferenceEquals(type, other.type) && isNullable == other.isNullable;
    }

    internal bool IsNullableType() {
        return type.IsNullableType();
    }

    internal bool IsVoidType() {
        return type.IsVoidType();
    }

    internal bool IsAtLeastAsVisibleAs(Symbol symbol) {
        return type.IsAtLeastAsVisibleAs(symbol);
    }

    internal TypeWithAnnotations SetIsAnnotated() {
        var newType = CorLibrary.GetSpecialType(SpecialType.Nullable).Construct([new TypeOrConstant(type, false)]);
        return new TypeWithAnnotations(newType, true);
    }

    internal TypeOrConstant SubstituteType(TemplateMap templateMap) {
        var typeSymbol = type.StrippedType();
        var newType = templateMap.SubstituteType(typeSymbol).type;

        if (type.IsNullableType() && !newType.IsNullableType())
            newType = newType.SetIsAnnotated();

        if (!typeSymbol.IsTemplateParameter()) {
            if (typeSymbol.Equals(newType.type, TypeCompareKind.ConsiderEverything))
                return new TypeOrConstant(this);
            else if (typeSymbol.IsNullableType() && isNullable)
                return new TypeOrConstant(newType);

            return new TypeOrConstant(newType.type, isNullable);
        }

        if ((object)newType == (TemplateParameterSymbol)typeSymbol)
            return new TypeOrConstant(this);
        else if ((object)this == (TemplateParameterSymbol)typeSymbol)
            return new TypeOrConstant(newType);

        return new TypeOrConstant(newType.type, isNullable || newType.isNullable);
    }

    internal bool ApplyNullableTransforms(
        byte defaultTransformFlag,
        ImmutableArray<byte> transforms,
        ref int position,
        out TypeWithAnnotations result) {
        result = this;

        var oldTypeSymbol = type;
        byte transformFlag;

        // if (CodeGenerator.IsValueType(oldTypeSymbol))
        //     transformFlag = NullableContextExtensions.ObliviousAttributeValue;
        if (transforms.IsDefault)
            transformFlag = defaultTransformFlag;
        else if (position < transforms.Length)
            transformFlag = transforms[position++];
        else
            return false;

        if (!oldTypeSymbol.ApplyNullableTransforms(
            defaultTransformFlag,
            transforms,
            ref position,
            out var newTypeSymbol)) {
            return false;
        }

        // var newTypeSymbol = new TypeWithAnnotations(oldTypeSymbol, true).SetIsAnnotated().type;

        if ((object)oldTypeSymbol != newTypeSymbol)
            result = new TypeWithAnnotations(newTypeSymbol, result.isNullable);

        if (result.specialType == SpecialType.Void)
            return true;

        switch (transformFlag) {
            case NullableContextExtensions.AnnotatedAttributeValue:
                result = result.isNullable ? result : result.SetIsAnnotated();
                break;
            case NullableContextExtensions.NotAnnotatedAttributeValue:
                result = new TypeWithAnnotations(result.nullableUnderlyingTypeOrSelf);
                break;
            default:
                result = this;
                return false;
        }

        return true;
    }

    public string ToDisplayString(SymbolDisplayFormat format = null) {
        var emittedType = type.ToDisplayString(format);

        if (isNullable)
            return string.Concat(emittedType, "?");

        return emittedType;
    }

    public bool Equals(TypeWithAnnotations other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    public bool Equals(TypeWithAnnotations other, TypeCompareKind compareKind) {
        if (IsSameAs(other))
            return true;

        if (type is null) {
            if (other.type is not null)
                return false;
        } else if (other.type is null || !type.Equals(other.type, compareKind)) {
            return false;
        }

        if ((compareKind & TypeCompareKind.IgnoreNullability) == 0)
            return isNullable == other.isNullable;

        return true;
    }

    public override int GetHashCode() {
        if (type is null)
            return 0;

        return type.GetHashCode();
    }

    private string GetDebuggerDisplay() {
        return !hasType ? "<null>" : ToDisplayString(SymbolDisplayFormat.Everything);
    }
}
