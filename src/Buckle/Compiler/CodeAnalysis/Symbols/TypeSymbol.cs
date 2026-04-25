using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

#pragma warning disable CS0660

/// <summary>
/// A type symbol. This is just the base type name, not a full <see cref="Binding.BoundType" />.
/// </summary>
internal abstract class TypeSymbol : NamespaceOrTypeSymbol, ITypeSymbol {
    internal const string ImplicitTypeName = "<invalid-global-code>";

    private static readonly Func<TypeSymbol, TemplateParameterSymbol, bool, bool> ContainsTemplateParameterPredicate =
        (type, parameter, unused) => type.typeKind == TypeKind.TemplateParameter &&
        (parameter is null || Equals(type, parameter, TypeCompareKind.ConsiderEverything));

    private ImmutableHashSet<Symbol> _lazyAbstractMembers;

    public override SymbolKind kind => SymbolKind.NamedType;

    public abstract TypeKind typeKind { get; }

    public abstract bool isPrimitiveType { get; }

    public abstract bool isObjectType { get; }

    public virtual SpecialType specialType => SpecialType.None;

    internal new TypeSymbol originalDefinition => _originalTypeSymbolDefinition;

    private protected virtual TypeSymbol _originalTypeSymbolDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => _originalTypeSymbolDefinition;

    internal abstract NamedTypeSymbol baseType { get; }

    internal abstract bool isRefLikeType { get; }

    internal ImmutableHashSet<Symbol> abstractMembers {
        get {
            if (_lazyAbstractMembers is null)
                Interlocked.CompareExchange(ref _lazyAbstractMembers, ComputeAbstractMembers(), null);

            return _lazyAbstractMembers;
        }
    }

    internal TypeSymbol EffectiveType() {
        return typeKind == TypeKind.TemplateParameter ? ((TemplateParameterSymbol)this).effectiveBaseClass : this;
    }

    internal bool IsDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        if ((object)this == type)
            return false;

        var current = baseType;

        while ((object)current is not null) {
            if (type.Equals(current, compareKind))
                return true;

            current = current.baseType;
        }

        return false;
    }

    internal bool IsEqualToOrDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        return Equals(type, compareKind) || IsDerivedFrom(type, compareKind);
    }

    internal bool IsRefLikeOrAllowsRefLikeType() {
        return isRefLikeType/* || this is TemplateParameterSymbol { allowsRefLikeType: true }*/;
    }

    internal int TypeToIndex() {
        switch (specialType) {
            case SpecialType.Any: return 0;
            case SpecialType.String: return 1;
            case SpecialType.Bool: return 2;
            case SpecialType.Char: return 3;
            case SpecialType.Int: return 4;
            case SpecialType.Decimal: return 5;
            case SpecialType.Type: return 6;
            case SpecialType.Int8: return 7;
            case SpecialType.Int16: return 8;
            case SpecialType.Int32: return 9;
            case SpecialType.Int64: return 10;
            case SpecialType.UInt8: return 11;
            case SpecialType.UInt16: return 12;
            case SpecialType.UInt32: return 13;
            case SpecialType.UInt64: return 14;
            case SpecialType.Float32: return 15;
            case SpecialType.Float64: return 16;
            case SpecialType.Object: return 17;
            case SpecialType.Nullable:
                var underlyingType = GetNullableUnderlyingType();

                switch (underlyingType.specialType) {
                    case SpecialType.Any: return 18;
                    case SpecialType.String: return 19;
                    case SpecialType.Bool: return 20;
                    case SpecialType.Char: return 21;
                    case SpecialType.Int: return 22;
                    case SpecialType.Decimal: return 23;
                    case SpecialType.Type: return 24;
                    case SpecialType.Int8: return 25;
                    case SpecialType.Int16: return 26;
                    case SpecialType.Int32: return 27;
                    case SpecialType.Int64: return 28;
                    case SpecialType.UInt8: return 29;
                    case SpecialType.UInt16: return 30;
                    case SpecialType.UInt32: return 31;
                    case SpecialType.UInt64: return 32;
                    case SpecialType.Float32: return 33;
                    case SpecialType.Float64: return 34;
                    case SpecialType.Object: return 35;
                }

                goto default;
            default: return -1;
        }
    }

    internal bool HasDefaultValue() {
        if (this.IsNullableType() || LiteralUtilities.TypeHasDefaultValue(specialType) || IsStructType())
            return true;

        if (this is TemplateParameterSymbol t) {
            if (t.hasNotNullConstraint)
                return false;

            return true;
        }

        return false;
    }

    internal bool IsErrorType() {
        return kind == SymbolKind.ErrorType;
    }

    internal bool IsClassType() {
        return typeKind == TypeKind.Class;
    }

    internal bool IsArray() {
        return typeKind == TypeKind.Array;
    }

    internal bool IsStructType() {
        return typeKind == TypeKind.Struct;
    }

    internal bool IsEnumType() {
        return typeKind == TypeKind.Enum;
    }

    internal bool IsTemplateParameter() {
        return typeKind == TypeKind.TemplateParameter;
    }

    internal bool IsPrimitiveType() {
        return specialType.IsPrimitiveType();
    }

    internal bool IsVoidType() {
        // TODO Use originalDefinition here?
        return specialType == SpecialType.Void;
    }

    internal TypeSymbol GetNonErrorGuess() {
        return ExtendedErrorTypeSymbol.ExtractNonErrorType(this);
    }

    internal abstract bool ApplyNullableTransforms(
        byte defaultTransformFlag,
        ImmutableArray<byte> transforms,
        ref int position,
        out TypeSymbol result);

    internal TypeSymbol UnderlyingTemplateTypeOrSelf() {
        if (kind != SymbolKind.TemplateParameter)
            return this;

        var underlyingType = ((TemplateParameterSymbol)this).underlyingType;

        if (underlyingType.specialType == SpecialType.Type)
            return this;

        return underlyingType.type;
    }

    internal bool ContainsErrorType() {
        var result = VisitType(
            (type, unused1, unused2) => type.IsErrorType(),
            (object?)null,
            canDigThroughNullable: true
        );

        return result is not null;
    }

    internal bool ContainsTemplateParameter(TemplateParameterSymbol parameter = null) {
        var result = VisitType(ContainsTemplateParameterPredicate, parameter);
        return result is not null;
    }

    internal TypeSymbol VisitType<T>(
        Func<TypeSymbol, T, bool, bool> predicate,
        T arg,
        bool canDigThroughNullable = false) {
        return TypeWithAnnotationsExtensions.VisitType(null, this, null, predicate, arg, canDigThroughNullable);
    }

    internal bool IsAtLeastAsVisibleAs(Symbol symbol) {
        return typeKind switch {
            TypeKind.Class or TypeKind.Struct => symbol.declaredAccessibility switch {
                Accessibility.Public => declaredAccessibility is Accessibility.Public or Accessibility.NotApplicable,
                Accessibility.Protected => declaredAccessibility is
                    Accessibility.Public or Accessibility.Protected or Accessibility.NotApplicable,
                _ => true,
            },
            _ => true,
        };
    }

    internal TypeSymbol GetNextBaseType(
        ConsList<TypeSymbol> basesBeingResolved,
        ref PooledHashSet<NamedTypeSymbol> visited) {
        switch (typeKind) {
            case TypeKind.TemplateParameter:
                return ((TemplateParameterSymbol)this).effectiveBaseClass;
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Error:
                return GetNextDeclaredBase((NamedTypeSymbol)this, basesBeingResolved, ref visited);
            case TypeKind.Array:
            case TypeKind.Enum:
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
            case TypeKind.Function:
                return baseType;
            case TypeKind.Primitive:
                return null;
            default:
                throw ExceptionUtilities.UnexpectedValue(typeKind);
        }
    }

    internal bool IsPossiblyNullableTypeTemplateParameter() {
        return this is TemplateParameterSymbol t && t.underlyingType.isNullable;
    }

    internal TypeSymbol GetNullableUnderlyingType() {
        return GetNullableUnderlyingTypeWithAnnotations().type;
    }

    internal TypeWithAnnotations GetNullableUnderlyingTypeWithAnnotations() {
        return ((NamedTypeSymbol)this).templateArguments[0].type;
    }

    internal TypeSymbol StrippedType() {
        return this.IsNullableType() ? GetNullableUnderlyingType() : this;
    }

    internal bool InheritsFromIgnoringConstruction(NamedTypeSymbol baseType) {
        var current = this;

        while (current is not null) {
            if ((object)current == baseType)
                return true;

            current = current.baseType?.originalDefinition;
        }

        return false;
    }


    private static TypeSymbol GetNextDeclaredBase(
        NamedTypeSymbol type,
        ConsList<TypeSymbol> basesBeingResolved,
        ref PooledHashSet<NamedTypeSymbol> visited) {
        if (basesBeingResolved is not null && basesBeingResolved.ContainsReference(type.originalDefinition))
            return null;

        if (type.specialType == SpecialType.Object) {
            type.SetKnownToHaveNoDeclaredBaseCycles();
            return null;
        }

        var nextType = type.GetDeclaredBaseType(basesBeingResolved);

        if (nextType is null) {
            SetKnownToHaveNoDeclaredBaseCycles(ref visited);
            return GetDefaultBaseOrNull(type);
        }

        var origType = type.originalDefinition;
        if (nextType.knownToHaveNoDeclaredBaseCycles) {
            origType.SetKnownToHaveNoDeclaredBaseCycles();
            SetKnownToHaveNoDeclaredBaseCycles(ref visited);
        } else {
            visited ??= PooledHashSet<NamedTypeSymbol>.GetInstance();
            visited.Add(origType);

            if (visited.Contains(nextType.originalDefinition))
                return GetDefaultBaseOrNull(type);
        }

        return nextType;
    }

    private static void SetKnownToHaveNoDeclaredBaseCycles(ref PooledHashSet<NamedTypeSymbol> visited) {
        if (visited is not null) {
            foreach (var v in visited)
                v.SetKnownToHaveNoDeclaredBaseCycles();

            visited.Free();
            visited = null;
        }
    }

    private static NamedTypeSymbol GetDefaultBaseOrNull(NamedTypeSymbol type) {
        switch (type.typeKind) {
            case TypeKind.Class:
            case TypeKind.Error:
                return CorLibrary.GetSpecialType(SpecialType.Object);
            case TypeKind.Struct:
                return null;
            default:
                throw ExceptionUtilities.UnexpectedValue(type.typeKind);
        }
    }

    private ImmutableHashSet<Symbol> ComputeAbstractMembers() {
        var abstractMembers = ImmutableHashSet.Create<Symbol>();
        var overriddenMembers = ImmutableHashSet.Create<Symbol>();

        foreach (var member in GetMembersUnordered()) {
            if (isAbstract && member.isAbstract && member.kind != SymbolKind.NamedType)
                abstractMembers = abstractMembers.Add(member);

            Symbol overriddenMember = null;
            switch (member.kind) {
                case SymbolKind.Method: {
                        overriddenMember = ((MethodSymbol)member).overriddenMethod;
                        break;
                    }
            }

            if (overriddenMember is not null)
                overriddenMembers = overriddenMembers.Add(overriddenMember);
        }

        if (baseType is not null && baseType.isAbstract) {
            foreach (var baseAbstractMember in baseType.abstractMembers) {
                if (!overriddenMembers.Contains(baseAbstractMember))
                    abstractMembers = abstractMembers.Add(baseAbstractMember);
            }
        }

        return abstractMembers;
    }

    internal virtual bool Equals(TypeSymbol other, TypeCompareKind compareKind) {
        return ReferenceEquals(this, other);
    }

    internal sealed override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (other is not TypeSymbol otherAsType)
            return false;

        return Equals(otherAsType, compareKind);
    }

    internal static bool Equals(TypeSymbol left, TypeSymbol right, TypeCompareKind compareKind) {
        if (left is null)
            return right is null;

        return left.Equals(right, compareKind);
    }

    public override int GetHashCode() {
        return RuntimeHelpers.GetHashCode(this);
    }

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator ==(TypeSymbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator !=(TypeSymbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator ==(Symbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator !=(Symbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator ==(TypeSymbol left, Symbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator !=(TypeSymbol left, Symbol right)
        => throw ExceptionUtilities.Unreachable();

    public ImmutableArray<ISymbol> GetMembersPublic() {
        return [.. GetMembers()];
    }

    INamedTypeSymbol ITypeSymbol.baseType => baseType;
}
