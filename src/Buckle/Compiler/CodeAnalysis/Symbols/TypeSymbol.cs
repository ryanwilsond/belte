using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

#pragma warning disable CS0660

/// <summary>
/// A type symbol. This is just the base type name, not a full <see cref="Binding.BoundType" />.
/// </summary>
internal abstract partial class TypeSymbol : NamespaceOrTypeSymbol, ITypeSymbol {
    private static readonly InterfaceInfo NoInterfaces = new InterfaceInfo();
    private InterfaceInfo _lazyInterfaceInfo;

    internal const string ImplicitTypeName = "<invalid-global-code>";

    private static readonly Func<TypeSymbol, TemplateParameterSymbol, bool, bool> ContainsTemplateParameterPredicate =
        (type, parameter, unused) => type.typeKind == TypeKind.TemplateParameter &&
        (parameter is null || Equals(type, parameter, TypeCompareKind.ConsiderEverything));

    private ImmutableHashSet<Symbol> _lazyAbstractMembers;

    public override SymbolKind kind => SymbolKind.NamedType;

    public abstract TypeKind typeKind { get; }

    public abstract bool isValueType { get; }

    public abstract bool isReferenceType { get; }

    public virtual bool hasDefault => HasDefaultValue();

    public virtual SpecialType specialType => SpecialType.None;

    internal new TypeSymbol originalDefinition => _originalTypeSymbolDefinition;

    private protected virtual TypeSymbol _originalTypeSymbolDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => _originalTypeSymbolDefinition;

    internal abstract NamedTypeSymbol baseType { get; }

    internal abstract bool isRefLikeType { get; }

    internal virtual bool isTupleType => false;

    internal virtual bool hasStructDefault => IsStructType();

    internal virtual ImmutableArray<string> tupleElementNames => [];

    internal virtual ImmutableArray<TypeOrConstant> tupleElementTypes => [];

    internal virtual ImmutableArray<FieldSymbol> tupleElements => [];

    internal ImmutableHashSet<Symbol> abstractMembers {
        get {
            if (_lazyAbstractMembers is null)
                Interlocked.CompareExchange(ref _lazyAbstractMembers, ComputeAbstractMembers(), null);

            return _lazyAbstractMembers;
        }
    }

    internal ImmutableArray<NamedTypeSymbol> allInterfaces => GetAllInterfaces();

    internal MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> interfacesAndTheirBaseInterfaces {
        get {
            var info = GetInterfaceInfo();

            if (info == NoInterfaces)
                return InterfaceInfo.EmptyInterfacesAndTheirBaseInterfaces;

            if (info.interfacesAndTheirBaseInterfaces is null) {
                Interlocked.CompareExchange(
                    ref info.interfacesAndTheirBaseInterfaces,
                    MakeInterfacesAndTheirBaseInterfaces(Interfaces()),
                    null
                );
            }

            return info.interfacesAndTheirBaseInterfaces;
        }
    }

    private static MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> MakeInterfacesAndTheirBaseInterfaces(
        ImmutableArray<NamedTypeSymbol> declaredInterfaces) {
        var resultBuilder = new MultiDictionary<NamedTypeSymbol, NamedTypeSymbol>(
            declaredInterfaces.Length,
            SymbolEqualityComparer.CLRSignature,
            SymbolEqualityComparer.ConsiderEverything
        );

        foreach (var @interface in declaredInterfaces) {
            if (resultBuilder.Add(@interface, @interface)) {
                foreach (var baseInterface in @interface.allInterfaces)
                    resultBuilder.Add(baseInterface, baseInterface);
            }
        }

        return resultBuilder;
    }

    private protected virtual ImmutableArray<NamedTypeSymbol> GetAllInterfaces() {
        var info = GetInterfaceInfo();

        if (info == NoInterfaces)
            return [];

        if (info.allInterfaces.IsDefault)
            ImmutableInterlocked.InterlockedInitialize(ref info.allInterfaces, MakeAllInterfaces());

        return info.allInterfaces;
    }

    private InterfaceInfo GetInterfaceInfo() {
        var info = _lazyInterfaceInfo;

        if (info is not null)
            return info;

        for (var baseType = this; baseType is not null; baseType = baseType.baseType) {
            var interfaces = (baseType.typeKind == TypeKind.TemplateParameter)
                ? ((TemplateParameterSymbol)baseType).effectiveInterfaces
                : baseType.Interfaces();

            if (!interfaces.IsEmpty) {
                info = new InterfaceInfo();
                return Interlocked.CompareExchange(ref _lazyInterfaceInfo, info, null) ?? info;
            }
        }

        _lazyInterfaceInfo = info = NoInterfaces;
        return info;
    }

    private protected virtual ImmutableArray<NamedTypeSymbol> MakeAllInterfaces() {
        var result = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        var visited = new HashSet<NamedTypeSymbol>(SymbolEqualityComparer.ConsiderEverything);

        for (var baseType = this; baseType is not null; baseType = baseType.baseType) {
            var interfaces = (baseType.typeKind == TypeKind.TemplateParameter)
                ? ((TemplateParameterSymbol)baseType).effectiveInterfaces
                : baseType.Interfaces();

            for (var i = interfaces.Length - 1; i >= 0; i--)
                AddAllInterfaces(interfaces[i], visited, result);
        }

        result.ReverseContents();
        return result.ToImmutableAndFree();

        static void AddAllInterfaces(
            NamedTypeSymbol @interface,
            HashSet<NamedTypeSymbol> visited,
            ArrayBuilder<NamedTypeSymbol> result) {
            if (visited.Add(@interface)) {
                var baseInterfaces = @interface.Interfaces();

                for (var i = baseInterfaces.Length - 1; i >= 0; i--) {
                    var baseInterface = baseInterfaces[i];
                    AddAllInterfaces(baseInterface, visited, result);
                }

                result.Add(@interface);
            }
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

    internal abstract ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved = null);

    internal bool IsEqualToOrDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        return Equals(type, compareKind) || IsDerivedFrom(type, compareKind);
    }

    internal bool CanUnifyWith(TypeSymbol otherType) {
        return TypeUnification.CanUnify(this, otherType);
    }

    internal bool IsRefLikeOrAllowsRefLikeType() {
        return isRefLikeType || this is TemplateParameterSymbol { allowsRefLikeType: true };
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
            case SpecialType.WinBool: return 18;
            case SpecialType.Nullable:
                var underlyingType = GetNullableUnderlyingType();

                switch (underlyingType.specialType) {
                    case SpecialType.Any: return 19;
                    case SpecialType.String: return 20;
                    case SpecialType.Bool: return 21;
                    case SpecialType.Char: return 22;
                    case SpecialType.Int: return 23;
                    case SpecialType.Decimal: return 24;
                    case SpecialType.Type: return 25;
                    case SpecialType.Int8: return 26;
                    case SpecialType.Int16: return 27;
                    case SpecialType.Int32: return 28;
                    case SpecialType.Int64: return 29;
                    case SpecialType.UInt8: return 30;
                    case SpecialType.UInt16: return 31;
                    case SpecialType.UInt32: return 32;
                    case SpecialType.UInt64: return 33;
                    case SpecialType.Float32: return 34;
                    case SpecialType.Float64: return 35;
                    case SpecialType.Object: return 36;
                    case SpecialType.WinBool: return 37;
                }

                goto default;
            default: return -1;
        }
    }

    internal bool HasDefaultValue() {
        if (this.IsNullableType() || LiteralUtilities.TypeHasDefaultValue(specialType))
            return true;

        if (IsStructType() && hasStructDefault)
            return true;

        if (this is TemplateParameterSymbol t)
            return t.hasDefault;

        if (IsEnumType())
            return this.GetEnumUnderlyingType().HasDefaultValue();

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

    internal bool IsTupleTypeOfCardinality(int targetCardinality) {
        if (isTupleType)
            return tupleElementTypes.Length == targetCardinality;

        return false;
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
            TypeKind.Class or TypeKind.Struct or TypeKind.Interface => symbol.declaredAccessibility switch {
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
            case TypeKind.Interface:
                return GetNextDeclaredBase((NamedTypeSymbol)this, basesBeingResolved, ref visited);
            case TypeKind.Array:
            case TypeKind.Enum:
            case TypeKind.Primitive:
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
            case TypeKind.Function:
                return baseType;
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

    private protected MultiDictionary<Symbol, Symbol>.ValueSet GetExplicitImplementationForInterfaceMember(
        Symbol interfaceMember) {
        var info = GetInterfaceInfo();

        if (info == NoInterfaces)
            return default;

        if (info.explicitInterfaceImplementationMap is null) {
            Interlocked.CompareExchange(
                ref info.explicitInterfaceImplementationMap,
                MakeExplicitInterfaceImplementationMap(),
                null
            );
        }

        return info.explicitInterfaceImplementationMap[interfaceMember];
    }

    private MultiDictionary<Symbol, Symbol> MakeExplicitInterfaceImplementationMap() {
        var map = new MultiDictionary<Symbol, Symbol>(ExplicitInterfaceImplementationTargetMemberEqualityComparer.Instance);

        foreach (var member in GetMembersUnordered()) {
            foreach (var interfaceMember in member.GetExplicitInterfaceImplementations())
                map.Add(interfaceMember, member);
        }

        return map;
    }

    internal SymbolAndDiagnostics FindImplementationForInterfaceMemberInNonInterface(
        Symbol interfaceMember,
        bool ignoreImplementationInInterfacesIfResultIsNotReady = false) {
        if (this.IsInterfaceType())
            return SymbolAndDiagnostics.Empty;

        var interfaceType = interfaceMember.containingType;

        if (interfaceType is null || !interfaceType.isInterface)
            return SymbolAndDiagnostics.Empty;

        switch (interfaceMember.kind) {
            case SymbolKind.Method:
                var info = GetInterfaceInfo();

                if (info == NoInterfaces)
                    return SymbolAndDiagnostics.Empty;

                var map = info.implementationForInterfaceMemberMap;
                SymbolAndDiagnostics result;

                if (map.TryGetValue(interfaceMember, out result))
                    return result;

                result = ComputeImplementationAndDiagnosticsForInterfaceMember(
                    interfaceMember,
                    ignoreImplementationInInterfaces: ignoreImplementationInInterfacesIfResultIsNotReady,
                    out var implementationInInterfacesMightChangeResult
                );

                if (!implementationInInterfacesMightChangeResult)
                    map.TryAdd(interfaceMember, result);

                return result;
            default:
                return SymbolAndDiagnostics.Empty;
        }
    }

    private SymbolAndDiagnostics ComputeImplementationAndDiagnosticsForInterfaceMember(
        Symbol interfaceMember,
        bool ignoreImplementationInInterfaces,
        out bool implementationInInterfacesMightChangeResult) {
        var diagnostics = new BelteDiagnosticQueue();

        var implementingMember = ComputeImplementationForInterfaceMember(
            interfaceMember,
            this,
            diagnostics,
            ignoreImplementationInInterfaces,
            out implementationInInterfacesMightChangeResult
        );

        var implementingMemberAndDiagnostics = new SymbolAndDiagnostics(implementingMember, diagnostics);
        return implementingMemberAndDiagnostics;
    }

    private static Symbol ComputeImplementationForInterfaceMember(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        BelteDiagnosticQueue diagnostics,
        bool ignoreImplementationInInterfaces,
        out bool implementationInInterfacesMightChangeResult) {
        var interfaceType = interfaceMember.containingType;
        var seenTypeDeclaringInterface = false;
        var implementingTypeIsFromSomeCompilation = false;

        Symbol implicitImpl = null;
        Symbol closestMismatch = null;
        var canBeImplementedImplicitly = interfaceMember.declaredAccessibility == Accessibility.Public;
        TypeSymbol implementingBaseOpt = null;
        var implementingTypeImplementsInterface = false;

        for (var currType = implementingType; currType is not null; currType = currType.baseType) {
            var explicitImpl = currType.GetExplicitImplementationForInterfaceMember(interfaceMember);

            if (explicitImpl.Count == 1) {
                implementationInInterfacesMightChangeResult = false;
                return explicitImpl.Single();
            } else if (explicitImpl.Count > 1) {
                if ((object)currType == implementingType || implementingTypeImplementsInterface)
                    diagnostics.Push(Error.DuplicateExplicitImpl(implementingType.location, interfaceMember));

                implementationInInterfacesMightChangeResult = false;
                return null;
            }

            var checkPendingExplicitImplementations = (object)currType != implementingType || !currType.isDefinition;

            if (checkPendingExplicitImplementations &&
                interfaceMember is MethodSymbol interfaceMethod &&
                currType.interfacesAndTheirBaseInterfaces.ContainsKey(interfaceType)) {
                var bodyOfSynthesizedMethodImpl = currType.GetBodyOfSynthesizedInterfaceMethodImpl(interfaceMethod);

                if (bodyOfSynthesizedMethodImpl is not null) {
                    implementationInInterfacesMightChangeResult = false;
                    return bodyOfSynthesizedMethodImpl;
                }
            }

            if (!seenTypeDeclaringInterface || (!canBeImplementedImplicitly && implementingBaseOpt is null)) {
                if (currType.interfacesAndTheirBaseInterfaces.ContainsKey(interfaceType)) {
                    if (!seenTypeDeclaringInterface) {
                        implementingTypeIsFromSomeCompilation =
                            currType.originalDefinition.containingModule is not PEModuleSymbol;
                        seenTypeDeclaringInterface = true;
                    }

                    if ((object)currType == implementingType)
                        implementingTypeImplementsInterface = true;
                    else if (!canBeImplementedImplicitly && implementingBaseOpt is null)
                        implementingBaseOpt = currType;
                }
            }

            if (seenTypeDeclaringInterface && (!interfaceMember.isStatic || implementingTypeIsFromSomeCompilation)) {
                FindPotentialImplicitImplementationMemberDeclaredInType(
                    interfaceMember,
                    implementingTypeIsFromSomeCompilation,
                    currType,
                    out var currTypeImplicitImpl,
                    out var currTypeCloseMismatch);

                if (currTypeImplicitImpl is not null) {
                    implicitImpl = currTypeImplicitImpl;
                    break;
                }

                closestMismatch ??= currTypeCloseMismatch;
            }
        }

        var tryDefaultInterfaceImplementation = true;

        if (implementingTypeIsFromSomeCompilation && implicitImpl is MethodSymbol implicitImplMethod &&
            implicitImplMethod.IsOperator() != ((MethodSymbol)interfaceMember).IsOperator()) {
            closestMismatch = implicitImpl;
            implicitImpl = null;
            tryDefaultInterfaceImplementation = false;
        }

        Symbol defaultImpl = null;

        if (implicitImpl is null && seenTypeDeclaringInterface && tryDefaultInterfaceImplementation) {
            if (ignoreImplementationInInterfaces) {
                implementationInInterfacesMightChangeResult = true;
            } else {
                defaultImpl = FindMostSpecificImplementationInInterfaces(
                    interfaceMember,
                    implementingType,
                    diagnostics
                );

                implementationInInterfacesMightChangeResult = false;
            }
        } else {
            implementationInInterfacesMightChangeResult = false;
        }

        if (defaultImpl is not null) {
            if (implementingTypeImplementsInterface) {
                ReportDefaultInterfaceImplementationMatchDiagnostics(
                    interfaceMember,
                    implementingType,
                    defaultImpl,
                    diagnostics
                );
            }

            return defaultImpl;
        }

        if (implementingTypeImplementsInterface) {
            if (implicitImpl is not null) {
                var suppressRegularValidation = false;

                if (!canBeImplementedImplicitly && interfaceMember.kind == SymbolKind.Method &&
                    implementingBaseOpt is null) {
                    if (implementingType is NamedTypeSymbol named &&
                        !AccessCheck.IsSymbolAccessible(interfaceMember, named, throughType: null)) {
                        diagnostics.Push(Error.ImplicitImplementationOfInaccessibleInterfaceMember(
                            GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, implicitImpl),
                            implementingType,
                            interfaceMember,
                            implicitImpl
                        ));

                        suppressRegularValidation = true;
                    }
                }

                if (!suppressRegularValidation) {
                    ReportImplicitImplementationMatchDiagnostics(
                        interfaceMember,
                        implementingType,
                        implicitImpl,
                        diagnostics
                    );
                }
            } else if (closestMismatch is not null) {
                ReportImplicitImplementationMismatchDiagnostics(
                    interfaceMember,
                    implementingType,
                    closestMismatch,
                    diagnostics
                );
            }
        }

        return implicitImpl;
    }

    internal static TextLocation GetImplicitImplementationDiagnosticLocation(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        Symbol member) {
        if (Equals(member.containingType, implementingType, TypeCompareKind.ConsiderEverything)) {
            return member.location;
        } else {
            var @interface = interfaceMember.containingType;
            var snt = implementingType as SourceMemberContainerTypeSymbol;
            return snt?.GetImplementsLocation(@interface) ?? implementingType.location;
        }
    }

    private static void ReportImplicitImplementationMismatchDiagnostics(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        Symbol closestMismatch,
        BelteDiagnosticQueue diagnostics) {
        var interfaceLocation = GetInterfaceLocation(interfaceMember, implementingType);

        if (closestMismatch.isStatic != interfaceMember.isStatic) {
            if (closestMismatch.isStatic) {
                diagnostics.Push(Error.CloseUnimplementedInterfaceMemberStatic(
                    interfaceLocation,
                    implementingType,
                    interfaceMember,
                    closestMismatch
                ));
            } else {
                diagnostics.Push(Error.CloseUnimplementedInterfaceMemberNotStatic(
                    interfaceLocation,
                    implementingType,
                    interfaceMember,
                    closestMismatch
                ));
            }
        } else if (closestMismatch.declaredAccessibility != Accessibility.Public) {
            diagnostics.Push(Error.CloseUnimplementedInterfaceMemberNotPublic(
                interfaceLocation,
                implementingType,
                interfaceMember,
                closestMismatch
            ));
            // } else if (HaveInitOnlyMismatch(interfaceMember, closestMismatch)) {
            //     diagnostics.Add(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongInitOnly, interfaceLocation, implementingType, interfaceMember, closestMismatch);
        } else {
            var interfaceMemberRefKind = RefKind.None;
            TypeSymbol interfaceMemberReturnType;

            switch (interfaceMember.kind) {
                case SymbolKind.Method:
                    var method = (MethodSymbol)interfaceMember;
                    interfaceMemberRefKind = method.refKind;
                    interfaceMemberReturnType = method.returnType;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(interfaceMember.kind);
            }

            var hasRefReturnMismatch = false;

            switch (closestMismatch.kind) {
                case SymbolKind.Method:
                    hasRefReturnMismatch = ((MethodSymbol)closestMismatch).refKind != interfaceMemberRefKind;
                    break;
            }

            if (hasRefReturnMismatch) {
                diagnostics.Push(Error.CloseUnimplementedInterfaceMemberWrongRefReturn(
                    interfaceLocation,
                    implementingType,
                    interfaceMember,
                    closestMismatch
                ));
            } else if (interfaceMember is MethodSymbol interfaceMethod &&
                interfaceMethod.IsOperator() != ((MethodSymbol)closestMismatch).IsOperator()) {
                diagnostics.Push(Error.CloseUnimplementedInterfaceMemberOperatorMismatch(
                    interfaceLocation,
                    implementingType,
                    interfaceMember,
                    closestMismatch
                ));
            } else {
                diagnostics.Push(Error.CloseUnimplementedInterfaceMemberWrongReturnType(
                    interfaceLocation,
                    implementingType,
                    interfaceMember,
                    closestMismatch,
                    interfaceMemberReturnType
                ));
            }
        }
    }

    private static void ReportDefaultInterfaceImplementationMatchDiagnostics(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        Symbol implicitImpl,
        BelteDiagnosticQueue diagnostics) {
        if (interfaceMember.kind == SymbolKind.Method) {
            var isStatic = implicitImpl.isStatic;

            if (!isStatic && implementingType.isRefLikeType) {
                throw ExceptionUtilities.Unreachable();
                // diagnostics.Add(ErrorCode.ERR_RefStructDoesNotSupportDefaultInterfaceImplementationForMember,
                //                 GetInterfaceLocation(interfaceMember, implementingType),
                //                 implicitImpl, interfaceMember, implementingType);
            } else if (implementingType.containingModule != implicitImpl.containingModule) {
                // The default implementation is coming from a different module, which means that we probably didn't check
                // for the required runtime capability or language version
                // TODO Do we need any of this checking
                // var feature = isStatic ? MessageID.IDS_FeatureStaticAbstractMembersInInterfaces : MessageID.IDS_DefaultInterfaceImplementation;

                // LanguageVersion requiredVersion = feature.RequiredVersion();
                // LanguageVersion? availableVersion = implementingType.DeclaringCompilation?.LanguageVersion;
                // if (requiredVersion > availableVersion) {
                //     diagnostics.Add(ErrorCode.ERR_LanguageVersionDoesNotSupportInterfaceImplementationForMember,
                //                     GetInterfaceLocation(interfaceMember, implementingType),
                //                     implicitImpl, interfaceMember, implementingType,
                //                     feature.Localize(),
                //                     availableVersion.GetValueOrDefault().ToDisplayString(),
                //                     new CSharpRequiredLanguageVersion(requiredVersion));
                // }

                // if (!(isStatic ?
                //           implementingType.ContainingAssembly.RuntimeSupportsStaticAbstractMembersInInterfaces :
                //           implementingType.ContainingAssembly.RuntimeSupportsDefaultInterfaceImplementation)) {
                //     diagnostics.Add(isStatic ?
                //                         ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember :
                //                         ErrorCode.ERR_RuntimeDoesNotSupportDefaultInterfaceImplementationForMember,
                //                     GetInterfaceLocation(interfaceMember, implementingType),
                //                     implicitImpl, interfaceMember, implementingType);
                // }
            }
        }
    }

    private static void ReportImplicitImplementationMatchDiagnostics(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        Symbol implicitImpl,
        BelteDiagnosticQueue diagnostics) {
        var reportedAnError = false;

        if (interfaceMember.kind == SymbolKind.Method) {
            var interfaceMethod = (MethodSymbol)interfaceMember;

            var implicitImplMethod = (MethodSymbol)implicitImpl;

            // if (implicitImplMethod.isConditional) {
            // CS0629: Conditional member '{0}' cannot implement interface member '{1}' in type '{2}'
            // diagnostics.Add(ErrorCode.ERR_InterfaceImplementedByConditional, GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, implicitImpl), implicitImpl, interfaceMethod, implementingType);
            // } else
            if (implicitImplMethod.isStatic && implicitImplMethod.methodKind == MethodKind.Ordinary &&
                implicitImplMethod.GetUnmanagedCallersOnlyAttributeData(forceComplete: true) is not null) {
                diagnostics.Push(Error.InterfaceImplementedByUnmanagedCallersOnlyMethod(
                    GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, implicitImpl),
                    implicitImpl,
                    interfaceMethod,
                    implementingType
                ));
            } else if (ReportAnyMismatchedConstraints(
                interfaceMethod,
                implementingType,
                implicitImplMethod,
                diagnostics)) {
                reportedAnError = true;
            }
        }

        if (implicitImpl.ContainsTupleNames() &&
            MemberSignatureComparer.ConsideringTupleNamesCreatesDifference(implicitImpl, interfaceMember)) {
            diagnostics.Push(Error.ImplBadTupleNames(
                GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, implicitImpl),
                implicitImpl,
                interfaceMember
            ));

            reportedAnError = true;
        }

        if (!reportedAnError && implementingType.declaringCompilation is not null) {
            CheckModifierMismatchOnImplementingMember(
                implementingType,
                implicitImpl,
                interfaceMember,
                isExplicit: false,
                diagnostics
            );
        }

        // TODO Interfaces warning
        // if (!implicitImpl.containingType.isDefinition) {
        //     foreach (Symbol member in implicitImpl.containingType.GetMembers(implicitImpl.name)) {
        //         if (member.DeclaredAccessibility != Accessibility.Public || member == implicitImpl) {
        //             //do nothing - not an ambiguous implementation
        //         } else if (MemberSignatureComparer.RuntimeImplicitImplementationComparer.Equals(interfaceMember, member) && !member.IsAccessor()) {
        //             // CONSIDER: Dev10 does not seem to report this for indexers or their accessors.
        //             diagnostics.Add(ErrorCode.WRN_MultipleRuntimeImplementationMatches, GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, member), member, interfaceMember, implementingType);
        //         }
        //     }
        // }

        if (implicitImpl.isStatic && interfaceMember.containingModule != implementingType.containingModule) {
            // TODO Interfaces do we need this error checking
            // LanguageVersion requiredVersion = MessageID.IDS_FeatureStaticAbstractMembersInInterfaces.RequiredVersion();
            // LanguageVersion? availableVersion = implementingType.DeclaringCompilation?.LanguageVersion;
            // if (requiredVersion > availableVersion) {
            //     diagnostics.Add(ErrorCode.ERR_LanguageVersionDoesNotSupportInterfaceImplementationForMember,
            //                     GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, implicitImpl),
            //                     implicitImpl, interfaceMember, implementingType,
            //                     MessageID.IDS_FeatureStaticAbstractMembersInInterfaces.Localize(),
            //                     availableVersion.GetValueOrDefault().ToDisplayString(),
            //                     new CSharpRequiredLanguageVersion(requiredVersion));
            // }

            // if (!implementingType.ContainingAssembly.RuntimeSupportsStaticAbstractMembersInInterfaces) {
            //     diagnostics.Add(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember,
            //                     GetImplicitImplementationDiagnosticLocation(interfaceMember, implementingType, implicitImpl),
            //                     implicitImpl, interfaceMember, implementingType);
            // }
        }
    }

    internal static void CheckModifierMismatchOnImplementingMember(
        TypeSymbol implementingType,
        Symbol implementingMember,
        Symbol interfaceMember,
        bool isExplicit,
        BelteDiagnosticQueue diagnostics) {
        if (!implementingMember.isImplicitlyDeclared) {
            // TODO Bunch of random modifier warnings we could have here
            switch (interfaceMember.kind) {
                case SymbolKind.Method:
                    var implementingMethod = (MethodSymbol)implementingMember;
                    var implementedMethod = (MethodSymbol)interfaceMember;

                    if (implementedMethod.isTemplateMethod) {
                        implementedMethod = implementedMethod.Construct(
                            TemplateMap.TemplateParametersAsTypeOrConstants(implementingMethod.templateParameters)
                        );
                    }

                    // CheckMethodOverride(
                    //     implementingType,
                    //     implementedMethod,
                    //     implementingMethod,
                    //     isExplicit: isExplicit,
                    //     diagnostics
                    // );

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(interfaceMember.kind);
            }
        }
    }

    private static bool ReportAnyMismatchedConstraints(
        MethodSymbol interfaceMethod,
        TypeSymbol implementingType,
        MethodSymbol implicitImpl,
        BelteDiagnosticQueue diagnostics) {
        var result = false;
        var arity = interfaceMethod.arity;

        if (arity > 0) {
            var typeParameters1 = interfaceMethod.templateParameters;
            var typeParameters2 = implicitImpl.templateParameters;
            var indexedTypeParameters = IndexedTemplateParameterSymbol.Take(arity);

            var typeMap1 = new TemplateMap(typeParameters1, indexedTypeParameters);
            var typeMap2 = new TemplateMap(typeParameters2, indexedTypeParameters);

            var compareKind = TypeCompareKind.IgnoreTupleNames;

            for (var i = 0; i < arity; i++) {
                var typeParameter1 = typeParameters1[i];
                var typeParameter2 = typeParameters2[i];

                if (!MemberSignatureComparer.HaveSameConstraints(
                    typeParameter1,
                    typeMap1,
                    typeParameter2,
                    typeMap2,
                    compareKind)) {
                    diagnostics.Push(Error.ImplBadConstraints(
                        GetImplicitImplementationDiagnosticLocation(interfaceMethod, implementingType, implicitImpl),
                        typeParameter2.name,
                        implicitImpl,
                        typeParameter1.name,
                        interfaceMethod,
                        interfaceMethod.containingType.name
                    ));
                }
                // TODO This is useless, correct? :
                //  else if (!MemberSignatureComparer.HaveSameNullabilityInConstraints(typeParameter1, typeMap1, typeParameter2, typeMap2)) {
                //     diagnostics.Add(ErrorCode.WRN_NullabilityMismatchInConstraintsOnImplicitImplementation, GetImplicitImplementationDiagnosticLocation(interfaceMethod, implementingType, implicitImpl),
                //                     typeParameter2.Name, implicitImpl, typeParameter1.Name, interfaceMethod);
                // }
            }
        }

        return result;
    }

    private static Symbol FindMostSpecificImplementationInInterfaces(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        BelteDiagnosticQueue diagnostics) {
        var defaultImpl = FindMostSpecificImplementationInBases(
            interfaceMember,
            implementingType,
            out var conflict1,
            out var conflict2
        );

        if (conflict1 is not null) {
            diagnostics.Push(Error.MostSpecificImplementationIsNotFound(
                GetInterfaceLocation(interfaceMember, implementingType),
                interfaceMember,
                conflict1,
                conflict2
            ));
        }

        return defaultImpl;
    }

    private static TextLocation GetInterfaceLocation(Symbol interfaceMember, TypeSymbol implementingType) {
        var @interface = interfaceMember.containingType;

        SourceMemberContainerTypeSymbol snt = null;

        if (implementingType.interfacesAndTheirBaseInterfaces[@interface].Contains(@interface))
            snt = implementingType as SourceMemberContainerTypeSymbol;

        return snt?.GetImplementsLocation(@interface) ?? implementingType.location;
    }

    private static Symbol FindMostSpecificImplementationInBases(
        Symbol interfaceMember,
        TypeSymbol implementingType,
        out Symbol conflictingImplementation1,
        out Symbol conflictingImplementation2) {
        var allInterfaces = implementingType.allInterfaces;

        if (allInterfaces.IsEmpty) {
            conflictingImplementation1 = null;
            conflictingImplementation2 = null;
            return null;
        }

        return FindMostSpecificImplementationInBasesCore(
            interfaceMember,
            allInterfaces,
            out conflictingImplementation1,
            out conflictingImplementation2
        );

        static Symbol FindMostSpecificImplementationInBasesCore(
            Symbol interfaceMember,
            ImmutableArray<NamedTypeSymbol> allInterfaces,
            out Symbol conflictingImplementation1,
            out Symbol conflictingImplementation2) {
            var implementations = ArrayBuilder<(MultiDictionary<Symbol, Symbol>.ValueSet MethodSet, MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> Bases)>
                .GetInstance();

            foreach (var interfaceType in allInterfaces) {
                if (!interfaceType.isInterface)
                    continue;

                var candidate = FindImplementationInInterface(interfaceMember, interfaceType);

                if (candidate.Count == 0)
                    continue;

                for (var i = 0; i < implementations.Count; i++) {
                    var (methodSet, bases) = implementations[i];
                    var previous = methodSet.First();
                    var previousContainingType = previous.containingType;

                    if (previousContainingType.Equals(interfaceType, TypeCompareKind.CLRSignatureCompareOptions)) {
                        implementations[i] = (candidate, bases);
                        candidate = default;
                        break;
                    }

                    if (bases is null) {
                        bases = previousContainingType.interfacesAndTheirBaseInterfaces;
                        implementations[i] = (methodSet, bases);
                    }

                    if (bases.ContainsKey(interfaceType)) {
                        candidate = default;
                        break;
                    }
                }

                if (candidate.Count == 0)
                    continue;

                if (implementations.Count != 0) {
                    var bases = interfaceType.interfacesAndTheirBaseInterfaces;

                    for (var i = implementations.Count - 1; i >= 0; i--) {
                        if (bases.ContainsKey(implementations[i].MethodSet.First().containingType))
                            implementations.RemoveAt(i);
                    }

                    implementations.Add((candidate, bases));
                } else {
                    implementations.Add((candidate, null));
                }
            }

            Symbol result;

            switch (implementations.Count) {
                case 0:
                    result = null;
                    conflictingImplementation1 = null;
                    conflictingImplementation2 = null;
                    break;
                case 1:
                    var methodSet = implementations[0].MethodSet;

                    switch (methodSet.Count) {
                        case 1:
                            result = methodSet.Single();

                            if (result.isAbstract)
                                result = null;

                            break;
                        default:
                            result = null;
                            break;
                    }

                    conflictingImplementation1 = null;
                    conflictingImplementation2 = null;
                    break;
                default:
                    result = null;
                    conflictingImplementation1 = implementations[0].MethodSet.First();
                    conflictingImplementation2 = implementations[1].MethodSet.First();
                    break;
            }

            implementations.Free();
            return result;
        }
    }

    internal static MultiDictionary<Symbol, Symbol>.ValueSet FindImplementationInInterface(
        Symbol interfaceMember,
        NamedTypeSymbol interfaceType) {
        var containingType = interfaceMember.containingType;

        if (containingType.Equals(interfaceType, TypeCompareKind.CLRSignatureCompareOptions)) {
            if (!interfaceMember.isAbstract) {
                if (!containingType.Equals(interfaceType, TypeCompareKind.ConsiderEverything))
                    interfaceMember = interfaceMember.originalDefinition.SymbolAsMember(interfaceType);

                return new MultiDictionary<Symbol, Symbol>.ValueSet(interfaceMember);
            }

            return default;
        }

        return interfaceType.GetExplicitImplementationForInterfaceMember(interfaceMember);
    }

    private static void FindPotentialImplicitImplementationMemberDeclaredInType(
        Symbol interfaceMember,
        bool implementingTypeIsFromSomeCompilation,
        TypeSymbol currType,
        out Symbol implicitImpl,
        out Symbol closeMismatch) {
        implicitImpl = null;
        closeMismatch = null;

        bool? isOperator = null;

        if (interfaceMember is MethodSymbol { isStatic: true } interfaceMethod)
            isOperator = interfaceMethod.methodKind is MethodKind.Operator or MethodKind.Conversion;

        foreach (var member in currType.GetMembers(interfaceMember.name)) {
            if (member.kind == interfaceMember.kind) {
                if (isOperator.HasValue &&
                    (((MethodSymbol)member).methodKind is MethodKind.Operator or MethodKind.Conversion)
                        != isOperator.GetValueOrDefault()) {
                    continue;
                }

                if (IsInterfaceMemberImplementation(member, interfaceMember, implementingTypeIsFromSomeCompilation)) {
                    implicitImpl = member;
                    return;
                } else if (closeMismatch is null && implementingTypeIsFromSomeCompilation) {
                    if (MemberSignatureComparer.CloseImplicitImplementationComparer.Equals(interfaceMember, member))
                        closeMismatch = member;
                }
            }
        }
    }

    private static bool IsInterfaceMemberImplementation(
        Symbol candidateMember,
        Symbol interfaceMember,
        bool implementingTypeIsFromSomeCompilation) {
        if (candidateMember.declaredAccessibility != Accessibility.Public
            || candidateMember.isStatic != interfaceMember.isStatic) {
            return false;
            // } else if (HaveInitOnlyMismatch(candidateMember, interfaceMember)) {
            //     return false;
        } else if (implementingTypeIsFromSomeCompilation) {
            return MemberSignatureComparer.ImplicitImplementationComparer
                .Equals(interfaceMember, candidateMember);
        } else {
            return MemberSignatureComparer.RuntimeImplicitImplementationComparer
                .Equals(interfaceMember, candidateMember);
        }
    }

    private protected MethodSymbol GetBodyOfSynthesizedInterfaceMethodImpl(MethodSymbol interfaceMethod) {
        var info = GetInterfaceInfo();

        if (info == NoInterfaces)
            return null;

        if (info.synthesizedMethodImplMap == null)
            Interlocked.CompareExchange(ref info.synthesizedMethodImplMap, MakeSynthesizedMethodImplMap(), null);

        if (info.synthesizedMethodImplMap.TryGetValue(interfaceMethod, out var result))
            return result;

        return null;

        ImmutableDictionary<MethodSymbol, MethodSymbol> MakeSynthesizedMethodImplMap() {
            var map = ImmutableDictionary.CreateBuilder<MethodSymbol, MethodSymbol>(
                ExplicitInterfaceImplementationTargetMemberEqualityComparer.Instance);

            foreach ((var body, var implemented) in SynthesizedInterfaceMethodImpls())
                map.Add(implemented, body);

            return map.ToImmutable();
        }
    }

    internal bool ImplementsInterface(TypeSymbol superInterface) {
        foreach (var @interface in allInterfaces) {
            if (@interface.isInterface && Equals(@interface, superInterface, TypeCompareKind.ConsiderEverything))
                return true;
        }

        return false;
    }

    internal abstract IEnumerable<(MethodSymbol Body, MethodSymbol Implemented)> SynthesizedInterfaceMethodImpls();

    internal bool InheritsFromIgnoringConstruction(
        NamedTypeSymbol baseType,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        PooledHashSet<NamedTypeSymbol> interfacesLookedAt = null;
        ArrayBuilder<NamedTypeSymbol> baseInterfaces = null;

        var baseTypeIsInterface = baseType.isInterface;

        if (baseTypeIsInterface) {
            interfacesLookedAt = PooledHashSet<NamedTypeSymbol>.GetInstance();
            baseInterfaces = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        }

        PooledHashSet<NamedTypeSymbol> visited = null;
        var current = this;
        var result = false;

        while (current is not null) {
            if (baseTypeIsInterface == current.IsInterfaceType() &&
                current == (object)baseType) {
                result = true;
                break;
            }

            if (baseTypeIsInterface)
                GetBaseInterfaces(current, baseInterfaces, interfacesLookedAt, basesBeingResolved);

            var next = current.GetNextBaseType(basesBeingResolved, ref visited);

            if (next is null)
                current = null;
            else
                current = next.originalDefinition;
        }

        visited?.Free();

        if (!result && baseTypeIsInterface) {
            while (baseInterfaces.Count != 0) {
                var currentBase = baseInterfaces.Pop();

                if (!currentBase.isInterface)
                    continue;

                if (currentBase == (object)baseType) {
                    result = true;
                    break;
                }

                GetBaseInterfaces(currentBase, baseInterfaces, interfacesLookedAt, basesBeingResolved);
            }
        }

        interfacesLookedAt?.Free();
        baseInterfaces?.Free();
        return result;

        static void GetBaseInterfaces(
            TypeSymbol derived,
            ArrayBuilder<NamedTypeSymbol> baseInterfaces,
            PooledHashSet<NamedTypeSymbol> interfacesLookedAt,
            ConsList<TypeSymbol> basesBeingResolved) {
            if (basesBeingResolved is not null && basesBeingResolved.ContainsReference(derived))
                return;

            ImmutableArray<NamedTypeSymbol> declaredInterfaces;

            switch (derived) {
                case TemplateParameterSymbol typeParameter:
                    declaredInterfaces = typeParameter.allEffectiveInterfaces;
                    break;
                case NamedTypeSymbol namedType:
                    declaredInterfaces = namedType.GetDeclaredInterfaces(basesBeingResolved);
                    break;
                default:
                    declaredInterfaces = derived.Interfaces(basesBeingResolved);
                    break;
            }

            foreach (var @interface in declaredInterfaces) {
                var definition = @interface.originalDefinition;

                if (interfacesLookedAt.Add(definition))
                    baseInterfaces.Add(definition);
            }
        }
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
            case TypeKind.Interface:
                return null;
            case TypeKind.Struct:
                return CorLibrary.GetSpecialType(SpecialType.ValueType);
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
