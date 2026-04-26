using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A cast from any <see cref="BoundType" /> to any <see cref="BoundType" /> (can be the same).
/// </summary>
internal readonly partial struct Conversion : IEquatable<Conversion> {
    internal static Conversion None => new Conversion(ConversionKind.None);
    internal static Conversion Identity => new Conversion(ConversionKind.Identity);
    internal static Conversion Implicit => new Conversion(ConversionKind.Implicit);
    internal static Conversion DefaultLiteral => new Conversion(ConversionKind.DefaultLiteral);
    internal static Conversion ImplicitConstant => new Conversion(ConversionKind.ImplicitConstant);
    internal static Conversion ImplicitNullable => new Conversion(ConversionKind.ImplicitNullable);
    internal static Conversion ImplicitReference => new Conversion(ConversionKind.ImplicitReference);
    internal static Conversion ImplicitNumeric => new Conversion(ConversionKind.ImplicitNumeric);
    internal static Conversion NullLiteral => new Conversion(ConversionKind.NullLiteral);
    internal static Conversion AnyBoxing => new Conversion(ConversionKind.AnyBoxing);
    internal static Conversion Explicit => new Conversion(ConversionKind.Explicit);
    internal static Conversion ExplicitNullable => new Conversion(ConversionKind.ExplicitNullable);
    internal static Conversion ExplicitReference => new Conversion(ConversionKind.ExplicitReference);
    internal static Conversion ExplicitNumeric => new Conversion(ConversionKind.ExplicitNumeric);
    internal static Conversion ExplicitPointerToPointer => new Conversion(ConversionKind.ExplicitPointerToPointer);
    internal static Conversion ExplicitIntegerToPointer => new Conversion(ConversionKind.ExplicitIntegerToPointer);
    internal static Conversion ExplicitPointerToInteger => new Conversion(ConversionKind.ExplicitPointerToInteger);
    internal static Conversion ImplicitPointerToVoid => new Conversion(ConversionKind.ImplicitPointerToVoid);
    internal static Conversion ImplicitNullToPointer => new Conversion(ConversionKind.ImplicitNullToPointer);
    internal static Conversion ImplicitEnum => new Conversion(ConversionKind.ImplicitEnum);
    internal static Conversion ExplicitEnum => new Conversion(ConversionKind.ExplicitEnum);
    internal static Conversion AnyUnboxing => new Conversion(ConversionKind.AnyUnboxing);
    internal static Conversion ImplicitNullableWithIdentityUnderlying
        => new Conversion(ConversionKind.ImplicitNullable, IdentityUnderlying);
    internal static Conversion ExplicitNullableWithIdentityUnderlying
        => new Conversion(ConversionKind.ExplicitNullable, IdentityUnderlying);
    internal static Conversion ImplicitNullableWithImplicitConstantUnderlying
        => new Conversion(ConversionKind.ImplicitNullable, ImplicitConstantUnderlying);
    internal static Conversion ExplicitNullableWithImplicitConstantUnderlying
        => new Conversion(ConversionKind.ExplicitNullable, ImplicitConstantUnderlying);

    internal static ImmutableArray<Conversion> IdentityUnderlying => [Identity];
    internal static ImmutableArray<Conversion> ImplicitConstantUnderlying => [ImplicitConstant];

    private readonly UncommonData _uncommonData;

    internal Conversion(ConversionKind castKind) {
        kind = castKind;
    }

    internal Conversion(ConversionKind castKind, ImmutableArray<Conversion> nestedConversions) {
        kind = castKind;
        _uncommonData = new NestedUncommonData(nestedConversions);
    }

    internal Conversion(UserDefinedConversionResult conversionResult, bool isImplicit) {
        kind = conversionResult.kind == UserDefinedConversionResultKind.NoApplicableOperators
            ? ConversionKind.None
            : isImplicit ? ConversionKind.ImplicitUserDefined : ConversionKind.ExplicitUserDefined;

        _uncommonData = conversionResult.kind == UserDefinedConversionResultKind.NoApplicableOperators &&
            conversionResult.results.IsEmpty
            ? MethodUncommonData.NoApplicableOperators
            : new MethodUncommonData(
                conversionResult: conversionResult,
                conversionMethod: null
            );
    }

    internal Conversion(ConversionKind kind, MethodSymbol conversionMethod) {
        this.kind = kind;
        _uncommonData = new MethodUncommonData(
            conversionResult: default,
            conversionMethod: conversionMethod
        );
    }

    private Conversion(ConversionKind kind, UncommonData uncommonData = null) {
        this.kind = kind;
        _uncommonData = uncommonData;
    }

    internal ConversionKind kind { get; }

    internal ImmutableArray<Conversion> underlyingConversions
        => _uncommonData is NestedUncommonData { nestedConversions: var conversions } ? conversions : default;

    /// <summary>
    /// If a cast exists (otherwise you cant go from one type to the other).
    /// </summary>
    internal bool exists => kind != ConversionKind.None;

    /// <summary>
    /// If the <see cref="Conversion" /> is an identity cast.
    /// </summary>
    internal bool isIdentity => kind == ConversionKind.Identity;

    /// <summary>
    /// If the <see cref="Conversion" /> is an implicit cast.
    /// </summary>
    internal bool isImplicit => kind.IsImplicitCast();

    /// <summary>
    /// If the <see cref="Conversion" /> is an explicit cast.
    /// A <see cref="Conversion" /> cannot be implicit and explicit.
    /// </summary>
    internal bool isExplicit => exists && !isImplicit;

    internal bool isNullable => kind.IsNullableCast();

    internal bool isReference => kind is ConversionKind.ImplicitReference or ConversionKind.ExplicitReference;

    internal bool isBoxing => kind.IsBoxingCast();

    internal bool isUnboxing => kind.IsUnboxingCast();

    internal MethodSymbol method {
        get {
            switch (_uncommonData) {
                case MethodUncommonData methodUncommonData:
                    if (methodUncommonData.conversionMethod is not null)
                        return methodUncommonData.conversionMethod;

                    var conversionResult = methodUncommonData.conversionResult;

                    if (conversionResult.kind == UserDefinedConversionResultKind.Valid) {
                        var analysis = conversionResult.results[conversionResult.best];
                        return analysis.@operator;
                    }

                    break;
            }

            return null;
        }
    }

    public override bool Equals(object obj) {
        return obj is Conversion conversion && Equals(conversion);
    }

    public bool Equals(Conversion other) {
        return kind == other.kind /*&& this.method == other.method*/;
    }
    public override int GetHashCode() {
        // return Hash.Combine(this.method, (int)this.Kind);
        return (int)kind;
    }

    public static bool operator ==(Conversion left, Conversion right) {
        return left.Equals(right);
    }

    public static bool operator !=(Conversion left, Conversion right) {
        return !(left == right);
    }

    internal static Conversion CollapseConversion(Conversion conversion) {
        var current = conversion;

        while (true) {
            if (current.isExplicit)
                break;

            if (current.underlyingConversions != default)
                current = current.underlyingConversions[0];
            else
                break;
        }

        return current;
    }

    /// <summary>
    /// Classify what type of <see cref="Conversion" /> is required to go from one type to the other.
    /// </summary>
    internal static Conversion Classify(TypeSymbol source, TypeSymbol target) {
        if (target.IsErrorType() || source.IsErrorType())
            return Identity;

        // Handle nullable conversions
        if (source.IsNullableType() && !target.IsNullableType()) {
            var underlyingConversion = Classify(source.StrippedType(), target);

            if (underlyingConversion.exists)
                return new Conversion(ConversionKind.ExplicitNullable, [underlyingConversion]);
        }

        if (target.IsNullableType()) {
            var underlyingConversion = Classify(source.StrippedType(), target.StrippedType());

            if (underlyingConversion.isIdentity && !source.IsEnumType() &&
                !target.IsEnumType() && !source.IsVerifierValue()) {
                return underlyingConversion;
            }

            if (underlyingConversion.exists)
                return new Conversion(ConversionKind.ImplicitNullable, [underlyingConversion]);
        }

        // Handle enum conversions
        if (source.IsEnumType() && target.IsEnumType()) {
            var sourceUnderlying = (source as NamedTypeSymbol).enumUnderlyingType;
            var targetUnderlying = (target as NamedTypeSymbol).enumUnderlyingType;

            if (source.Equals(target))
                return Identity;

            if (sourceUnderlying.specialType == SpecialType.String || targetUnderlying.specialType == SpecialType.String)
                return None;

            return ExplicitEnum;
        }

        if (source.IsEnumType()) {
            if (target.specialType == SpecialType.Any)
                return Implicit;

            var underlying = (source as NamedTypeSymbol).enumUnderlyingType;
            var underlyingConversion = CollapseConversion(Classify(underlying, target));

            if (underlyingConversion.isImplicit)
                return ImplicitEnum;
            else if (underlyingConversion.exists)
                // TODO Consider making this ExplicitEnum
                return ImplicitEnum;

            return None;
        }

        if (target.IsEnumType()) {
            var underlying = (target as NamedTypeSymbol).enumUnderlyingType;
            var underlyingConversion = CollapseConversion(Classify(source, underlying));

            if (underlyingConversion.exists)
                // TODO Consider making this ExplicitEnum
                return ImplicitEnum;

            return None;
        }

        // Handle most primitive conversions
        if (source.typeKind == TypeKind.Primitive && target.typeKind == TypeKind.Primitive)
            return new Conversion(EasyOut.Classify(source, target));

        // Handle conversions with 'any' type that weren't picked up with the EasyOut
        // ~~We only allow using 'any' with other primitives, so the only type that the EasyOut couldn't pick up was array conversions~~
        // TODO We changed this to now treat any as a bigger-O Object
        if (target.specialType == SpecialType.Any && source.typeKind != TypeKind.Pointer)
            return Implicit;

        if (source.specialType == SpecialType.Any && target.specialType == SpecialType.Array)
            return Explicit;

        if (source.Equals(target, TypeCompareKind.ConsiderEverything))
            return Identity;

        if (source.IsPointerOrFunctionPointer() && target.IsPointerOrFunctionPointer()) {
            if (target is PointerTypeSymbol p && p.pointedAtType.specialType == SpecialType.Void)
                return ImplicitPointerToVoid;

            return ExplicitPointerToPointer;
        }

        if (source.specialType.IsNumeric() && target.typeKind == TypeKind.Pointer) {
            if (source.specialType is not SpecialType.Float32 and not SpecialType.Float64 and not SpecialType.Decimal)
                return ExplicitIntegerToPointer;
        }

        if (target.specialType.IsNumeric() && source.typeKind == TypeKind.Pointer) {
            if (target.specialType is not SpecialType.Float32 and not SpecialType.Float64 and not SpecialType.Decimal)
                return ExplicitPointerToInteger;
        }

        // EasyOut and our special array check covers all possible primitive conversions
        if (source.typeKind == TypeKind.Primitive || target.typeKind == TypeKind.Primitive)
            return None;

        if (target.isStatic)
            return None;

        if (source.kind is SymbolKind.NamedType or SymbolKind.TemplateParameter &&
            target.kind is SymbolKind.NamedType or SymbolKind.TemplateParameter) {
            if (IsBaseClass(source, target))
                return Implicit;

            if (IsBaseClass(target, source))
                return Explicit;
        }

        return None;
    }

    internal ListExpressionTypeKind GetListExpressionTypeKind(out TypeSymbol elementType) {
        if (_uncommonData is ListExpressionUncommonData listExpressionData) {
            elementType = listExpressionData.elementType;
            return listExpressionData.listExpressionTypeKind;
        }

        elementType = null;
        return ListExpressionTypeKind.None;
    }

    internal static Conversion CreateListExpressionConversion(
        ListExpressionTypeKind listExpressionTypeKind,
        TypeSymbol elementType,
        ImmutableArray<Conversion> elementConversions) {
        return new Conversion(
            ConversionKind.ListExpression,
            new ListExpressionUncommonData(listExpressionTypeKind, elementType, elementConversions)
        );
    }

    internal static Conversion MakeNullableConversion(ConversionKind kind, Conversion nestedConversion) {
        return nestedConversion.kind switch {
            ConversionKind.Identity => kind == ConversionKind.ImplicitNullable
                ? ImplicitNullableWithIdentityUnderlying
                : ExplicitNullableWithIdentityUnderlying,
            ConversionKind.ImplicitConstant => kind == ConversionKind.ImplicitNullable
                ? ImplicitNullableWithImplicitConstantUnderlying
                : ExplicitNullableWithImplicitConstantUnderlying,
            _ => new Conversion(kind, ImmutableArray.Create(nestedConversion)),
        };
    }

    internal static bool IsBaseClass(TypeSymbol derivedType, TypeSymbol baseType) {
        if (!baseType.IsClassType())
            return false;

        for (var b = BaseType(derivedType); (object)b is not null; b = BaseType(b)) {
            if (HasIdentityConversionInternal(b, baseType))
                return true;
        }

        return false;

        static TypeSymbol BaseType(TypeSymbol type) {
            return type.kind switch {
                SymbolKind.NamedType => ((NamedTypeSymbol)type).baseType,
                SymbolKind.TemplateParameter => ((TemplateParameterSymbol)type).effectiveBaseClass,
                _ => null
            };
        }
    }

    internal static bool HasIdentityOrImplicitConversion(TypeSymbol source, TypeSymbol destination) {
        if (HasIdentityConversionInternal(source, destination))
            return true;

        return HasImplicitConversion(source, destination);
    }

    internal static bool HasImplicitConversion(TypeSymbol source, TypeSymbol destination) {
        if (source.IsErrorType())
            return false;

        if (destination.specialType == SpecialType.Object)
            return true;

        switch (source.typeKind) {
            case TypeKind.Class:
                return IsBaseClass(source, destination);
            case TypeKind.TemplateParameter:
                return HasImplicitTypeParameterConversion((TemplateParameterSymbol)source, destination);
            case TypeKind.Array:
                return HasImplicitConversionFromArray(source, destination);
        }

        return false;
    }

    internal static bool HasTopLevelNullabilityImplicitConversion(
        TypeWithAnnotations source,
        TypeWithAnnotations destination) {
        if (destination.isNullable)
            return true;

        if (IsPossiblyNullableTypeTypeParameter(source) && !IsPossiblyNullableTypeTypeParameter(destination))
            return false;

        return !source.isNullable;
    }

    internal static bool HasBoxingConversion(TypeSymbol source, TypeSymbol destination) {
        if ((source.typeKind == TypeKind.TemplateParameter) &&
            HasImplicitBoxingTemplateParameterConversion((TemplateParameterSymbol)source, destination)) {
            return true;
        }

        // The rest of the boxing conversions only operate when going from a specific primitive type to the `any` type
        if (!source.isPrimitiveType || destination.originalDefinition.specialType != SpecialType.Any)
            return false;

        if (source.IsNullableType())
            return HasBoxingConversion(source.GetNullableUnderlyingType(), destination);

        // TODO Return false?
        return true;
    }

    private static bool HasImplicitBoxingTemplateParameterConversion(
        TemplateParameterSymbol source,
        TypeSymbol destination) {
        // TODO Does this conflict with the notion that "boxing" conversions have a destination of `any`?
        if (source.isObjectType)
            return false;

        if (HasImplicitEffectiveBaseConversion(source, destination))
            return true;

        return false;
    }

    private static bool IsPossiblyNullableTypeTypeParameter(TypeWithAnnotations typeWithAnnotations) {
        var type = typeWithAnnotations.type;
        return type is not null &&
            (type.IsPossiblyNullableTypeTemplateParameter() || type.IsNullableTypeOrTypeParameter());
    }

    private static bool HasImplicitConversion(TypeWithAnnotations source, TypeWithAnnotations destination) {
        if (!HasTopLevelNullabilityImplicitConversion(source, destination))
            return false;

        if (source.isNullable != destination.isNullable &&
            HasIdentityConversionInternal(source.type, destination.type, includeNullability: true)) {
            return true;
        }

        return HasImplicitConversion(source.type, destination.type);
    }

    private static bool HasImplicitTypeParameterConversion(
        TemplateParameterSymbol source,
        TypeSymbol destination) {
        if (HasImplicitEffectiveBaseConversion(source, destination))
            return true;

        return false;
    }

    private static bool HasImplicitConversionFromArray(TypeSymbol source, TypeSymbol destination) {
        if (source is not ArrayTypeSymbol)
            return false;

        if (HasCovariantArrayConversion(source, destination))
            return true;

        if (destination.GetSpecialTypeSafe() == SpecialType.Array)
            return true;

        return false;
    }

    private static bool HasCovariantArrayConversion(TypeSymbol source, TypeSymbol destination) {
        if (source is not ArrayTypeSymbol s || destination is not ArrayTypeSymbol d)
            return false;

        if (!s.HasSameShapeAs(d))
            return false;

        return HasImplicitConversion(s.elementTypeWithAnnotations, d.elementTypeWithAnnotations);
    }

    private static bool HasImplicitEffectiveBaseConversion(TemplateParameterSymbol source, TypeSymbol destination) {
        var effectiveBaseClass = source.effectiveBaseClass;
        return HasIdentityConversionInternal(effectiveBaseClass, destination) ||
            IsBaseClass(effectiveBaseClass, destination);
    }

    private static bool HasIdentityConversionInternal(TypeSymbol type1, TypeSymbol type2) {
        return HasIdentityConversionInternal(type1, type2, includeNullability: false);
    }

    private static bool HasIdentityConversionInternal(TypeSymbol type1, TypeSymbol type2, bool includeNullability) {
        var compareKind = includeNullability
            ? TypeCompareKind.AllIgnoreOptions & ~TypeCompareKind.IgnoreNullability
            : TypeCompareKind.AllIgnoreOptions;

        return type1.Equals(type2, compareKind);
    }
}
