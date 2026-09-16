using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourcePropertySymbolBase : PropertySymbol, IAttributeTargetSymbol {
    private readonly SourceMemberContainerTypeSymbol _containingType;
    private readonly string _name;
    private readonly SyntaxReference _syntaxRef;
    protected readonly DeclarationModifiers _modifiers;
    private readonly SourcePropertyAccessorSymbol _getMethod;
    private readonly SourcePropertyAccessorSymbol _setMethod;
    private readonly TypeSymbol _explicitInterfaceType;
    private ImmutableArray<PropertySymbol> _lazyExplicitInterfaceImplementations;
    private readonly Flags _propertyFlags;
    private readonly RefKind _refKind;

    private SymbolCompletionState _state;
    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TypeWithAnnotations _lazyType;

    private string _lazySourceName;

    private string _lazyDocComment;
    private string _lazyExpandedDocComment;
    private OverriddenOrHiddenMembersResult _lazyOverriddenOrHiddenMembers;
    private SynthesizedSealedPropertyAccessor _lazySynthesizedSealedAccessor;
    private CustomAttributesBag<AttributeData> _lazyCustomAttributesBag;

    private SynthesizedBackingFieldSymbol _lazyDeclaredBackingField;
    private StrongBox<SynthesizedBackingFieldSymbol> _lazyMergedBackingField;

    private protected SourcePropertySymbolBase(
        SourceMemberContainerTypeSymbol containingType,
        BelteSyntaxNode syntax,
        bool hasGetAccessor,
        bool hasSetAccessor,
        bool isExplicitInterfaceImplementation,
        TypeSymbol explicitInterfaceType,
        string aliasQualifierOpt,
        DeclarationModifiers modifiers,
        bool hasInitializer,
        bool hasExplicitAccessMod,
        bool hasAutoPropertyGet,
        bool hasAutoPropertySet,
        bool isExpressionBodied,
        bool accessorsHaveImplementation,
        bool getterUsesFieldKeyword,
        bool setterUsesFieldKeyword,
        RefKind refKind,
        string memberName,
        SyntaxList<AttributeListSyntax> indexerNameAttributeLists,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        Debug.Assert(!isExpressionBodied || !(hasAutoPropertyGet || hasAutoPropertySet));
        Debug.Assert(!isExpressionBodied || !hasInitializer);
        Debug.Assert(!isExpressionBodied || accessorsHaveImplementation);

        _syntaxRef = new SyntaxReference(syntax);
        this.location = location;
        _containingType = containingType;
        _refKind = refKind;
        _modifiers = modifiers;
        _explicitInterfaceType = explicitInterfaceType;

        if (isExplicitInterfaceImplementation)
            _propertyFlags |= Flags.IsExplicitInterfaceImplementation;
        else
            _lazyExplicitInterfaceImplementations = [];

        if (hasExplicitAccessMod)
            _propertyFlags |= Flags.HasExplicitAccessModifier;

        if (hasAutoPropertyGet)
            _propertyFlags |= Flags.HasAutoPropertyGet;

        if (hasAutoPropertySet)
            _propertyFlags |= Flags.HasAutoPropertySet;

        if (getterUsesFieldKeyword)
            _propertyFlags |= Flags.GetterUsesFieldKeyword;

        if (setterUsesFieldKeyword)
            _propertyFlags |= Flags.SetterUsesFieldKeyword;

        if (hasInitializer)
            _propertyFlags |= Flags.HasInitializer;

        if (isExpressionBodied)
            _propertyFlags |= Flags.IsExpressionBodied;

        if (accessorsHaveImplementation)
            _propertyFlags |= Flags.AccessorsHaveImplementation;

        _name = _lazySourceName = memberName;

        if (getterUsesFieldKeyword || setterUsesFieldKeyword ||
            hasAutoPropertyGet || hasAutoPropertySet || hasInitializer) {
            _propertyFlags |= Flags.RequiresBackingField;
        }

        if (hasGetAccessor)
            _getMethod = CreateGetAccessorSymbol(hasAutoPropertyGet, diagnostics);

        if (hasSetAccessor)
            _setMethod = CreateSetAccessorSymbol(hasAutoPropertySet, diagnostics);

        Debug.Assert(_lazyDeclaredBackingField is null);
    }

    private protected abstract TextLocation _typeLocation { get; }

    internal bool isExpressionBodied => (_propertyFlags & Flags.IsExpressionBodied) != 0;

    internal sealed override RefKind refKind => _refKind;

    internal sealed override TypeWithAnnotations typeWithAnnotations {
        get {
            EnsureSignature();
            return _lazyType;
        }
    }

    internal override TextLocation location { get; }

    public override string name => _name;

    public override string metadataName => sourceName.Replace(" ", "");

    internal override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    internal override ImmutableArray<TextLocation> locations => [location];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [_syntaxRef];

    internal override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    private protected bool _hasExternModifier => (_modifiers & DeclarationModifiers.Extern) != 0;

    internal override bool isExtern => _hasExternModifier;

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isOverride => (_modifiers & DeclarationModifiers.Override) != 0;

    internal override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal override bool isVirtual => (_modifiers & DeclarationModifiers.Virtual) != 0;

    internal bool isNew => (_modifiers & DeclarationModifiers.New) != 0;

    internal bool hasConstModifier => (_modifiers & DeclarationModifiers.Const) != 0;

    internal bool hasLowLevelModifier => (_modifiers & DeclarationModifiers.LowLevel) != 0;

    internal sealed override bool requiresCompletion => true;

    internal string sourceName {
        get {
            Debug.Assert(_lazySourceName is not null);
            return _lazySourceName;
        }
    }

    internal sealed override MethodSymbol getMethod => _getMethod;

    internal sealed override MethodSymbol setMethod => _setMethod;

    internal override CallingConvention callingConvention => isStatic ? 0 : CallingConvention.HasThis;

    internal sealed override ImmutableArray<ParameterSymbol> parameters {
        get {
            EnsureSignature();
            return _lazyParameters;
        }
    }

    internal override bool isExplicitInterfaceImplementation
        => (_propertyFlags & Flags.IsExplicitInterfaceImplementation) != 0;

    internal override bool hasSpecialName {
        get {
            var data = GetDecodedWellKnownAttributeData();
            return data is not null && data.hasSpecialNameAttribute;
        }
    }

    internal sealed override ImmutableArray<PropertySymbol> explicitInterfaceImplementations {
        get {
            if (isExplicitInterfaceImplementation)
                EnsureSignature();
            else
                Debug.Assert(_lazyExplicitInterfaceImplementations.IsEmpty);

            return _lazyExplicitInterfaceImplementations;
        }
    }

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal bool isAutoPropertyOrUsesFieldKeyword
        => IsSetOnEitherPart(
            Flags.HasAutoPropertyGet | Flags.HasAutoPropertySet |
            Flags.GetterUsesFieldKeyword | Flags.SetterUsesFieldKeyword
        );

    internal bool usesFieldKeyword
        => IsSetOnEitherPart(Flags.GetterUsesFieldKeyword | Flags.SetterUsesFieldKeyword);

    private protected bool _hasExplicitAccessModifier
        => (_propertyFlags & Flags.HasExplicitAccessModifier) != 0;

    internal bool isAutoProperty
        => IsSetOnEitherPart(Flags.HasAutoPropertyGet | Flags.HasAutoPropertySet);

    internal bool hasAutoPropertyGet
        => IsSetOnEitherPart(Flags.HasAutoPropertyGet);

    internal bool hasAutoPropertySet
        => IsSetOnEitherPart(Flags.HasAutoPropertySet);

    private protected bool _accessorsHaveImplementation
        => (_propertyFlags & Flags.AccessorsHaveImplementation) != 0;

    internal override OverriddenOrHiddenMembersResult overriddenOrHiddenMembers {
        get {
            if (_lazyOverriddenOrHiddenMembers is null) {
                Interlocked.CompareExchange(
                    ref _lazyOverriddenOrHiddenMembers,
                    this.MakeOverriddenOrHiddenMembers(),
                    null
                );
            }

            return _lazyOverriddenOrHiddenMembers;
        }
    }

    internal SynthesizedSealedPropertyAccessor synthesizedSealedAccessor {
        get {
            var hasGetter = getMethod is not null;
            var hasSetter = setMethod is not null;

            if (!isSealed || (hasGetter && hasSetter))
                return null;

            if (_lazySynthesizedSealedAccessor is null)
                Interlocked.CompareExchange(ref _lazySynthesizedSealedAccessor, MakeSynthesizedSealedAccessor(), null);

            return _lazySynthesizedSealedAccessor;
        }
    }

    internal SynthesizedBackingFieldSymbol backingField {
        get {
            if (_lazyMergedBackingField is null) {
                var backingField = declaredBackingField;

                Interlocked.CompareExchange(
                    ref _lazyMergedBackingField,
                    new StrongBox<SynthesizedBackingFieldSymbol>(backingField),
                    null
                );
            }

            return _lazyMergedBackingField.Value;
        }
    }

    internal SynthesizedBackingFieldSymbol declaredBackingField {
        get {
            if (_lazyDeclaredBackingField is null &&
                (_propertyFlags & Flags.RequiresBackingField) != 0) {
                Interlocked.CompareExchange(ref _lazyDeclaredBackingField, CreateBackingField(), null);
            }
            return _lazyDeclaredBackingField;
        }
    }

    internal override bool mustCallMethodsDirectly => false;

    internal override SyntaxReference syntaxReference => _syntaxRef;

    internal BelteSyntaxNode belteSyntaxNode => (BelteSyntaxNode)_syntaxRef.node;

    internal SyntaxTree syntaxTree => _syntaxRef.syntaxTree;

    internal abstract OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations();

    private protected abstract SourcePropertySymbolBase _boundAttributesSource { get; }

    internal abstract IAttributeTargetSymbol attributesOwner { get; }

    internal override LexicalSortKey GetLexicalSortKey() {
        return new LexicalSortKey(syntaxReference, declaringCompilation);
    }

    private protected abstract SourcePropertyAccessorSymbol CreateGetAccessorSymbol(
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics);

    private protected abstract SourcePropertyAccessorSymbol CreateSetAccessorSymbol(
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics);

    private protected void CheckInitializerIfNeeded(BelteDiagnosticQueue diagnostics) {
        if ((_propertyFlags & Flags.HasInitializer) == 0)
            return;

        if (containingType.isInterface && !isStatic) {
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_InstancePropertyInitializerInInterface, Location);
        } else if (!isAutoPropertyOrUsesFieldKeyword) {
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_InitializerOnNonAutoProperty, Location);
        }
    }

    private void EnsureSignatureGuarded(BelteDiagnosticQueue diagnostics) {
        PropertySymbol explicitlyImplementedProperty = null;

        (_lazyType, _lazyParameters) = MakeParametersAndBindType(diagnostics);

        var isExplicitInterfaceImplementation = this.isExplicitInterfaceImplementation;

        if (isExplicitInterfaceImplementation || isOverride) {
            var isOverride = false;
            PropertySymbol overriddenOrImplementedProperty;

            if (!isExplicitInterfaceImplementation) {
                isOverride = true;
                overriddenOrImplementedProperty = overriddenProperty;
            } else {
                var syntax = belteSyntaxNode;
                var interfacePropertyName = ((PropertyDeclarationSyntax)syntax).identifier.valueText;
                explicitlyImplementedProperty = this.FindExplicitlyImplementedProperty(_explicitInterfaceType, interfacePropertyName, GetExplicitInterfaceSpecifier(), diagnostics);
                this.FindExplicitlyImplementedMemberVerification(explicitlyImplementedProperty, diagnostics);
                overriddenOrImplementedProperty = explicitlyImplementedProperty;
            }

            if (overriddenOrImplementedProperty is not null) {
                var overriddenPropertyType = overriddenOrImplementedProperty.typeWithAnnotations;

                if (type.Equals(overriddenPropertyType.type, TypeCompareKind.IgnoreArraySizesAndLowerBounds))
                    _lazyType = new TypeWithAnnotations(type);

                // _lazyParameters = CustomModifierUtils.CopyParameterCustomModifiers(overriddenOrImplementedProperty.Parameters, _lazyParameters, alsoCopyParamsModifier: isOverride);
                _lazyParameters = overriddenOrImplementedProperty.parameters;
            }
        } else if (_refKind == RefKind.RefConst) {
            // var modifierType = Binder.GetWellKnownType(DeclaringCompilation, WellKnownType.System_Runtime_InteropServices_InAttribute, diagnostics, TypeLocation);
            // _lazyRefCustomModifiers = ImmutableArray.Create(CSharpCustomModifier.CreateRequired(modifierType));
        }

        Debug.Assert(isExplicitInterfaceImplementation || _lazyExplicitInterfaceImplementations.IsEmpty);
        _lazyExplicitInterfaceImplementations =
            explicitlyImplementedProperty is null
                ? []
                : [explicitlyImplementedProperty];
    }

    private static void CheckFieldKeywordUsage(SourcePropertySymbolBase property, BelteDiagnosticQueue diagnostics) {
        SourcePropertyAccessorSymbol? accessorToBlame = null;
        var propertyFlags = property._propertyFlags;
        var getterUsesFieldKeyword = (propertyFlags & Flags.GetterUsesFieldKeyword) != 0;
        var setterUsesFieldKeyword = (propertyFlags & Flags.SetterUsesFieldKeyword) != 0;

        if (property._setMethod is { isAutoPropertyAccessor: false } setMethod
            && !setterUsesFieldKeyword
            && !property.IsSetOnEitherPart(Flags.HasInitializer)
            && (property.hasAutoPropertyGet || getterUsesFieldKeyword)) {
            accessorToBlame = setMethod;
        } else if (property._getMethod is { isAutoPropertyAccessor: false } getMethod
              && !getterUsesFieldKeyword
              && (property.hasAutoPropertySet || setterUsesFieldKeyword)) {
            accessorToBlame = getMethod;
        }

        if (accessorToBlame is not null) {
            var accessorName = accessorToBlame switch {
                { methodKind: MethodKind.PropertyGet, isInitOnly: false } => SyntaxFacts.GetText(SyntaxKind.GetKeyword),
                { methodKind: MethodKind.PropertySet, isInitOnly: false } => SyntaxFacts.GetText(SyntaxKind.SetKeyword),
                // { methodKind: MethodKind.PropertySet, isInitOnly: true } => SyntaxFacts.GetText(SyntaxKind.InitKeyword),
                { methodKind: MethodKind.PropertySet, isInitOnly: true } => throw ExceptionUtilities.Unreachable(),
                _ => throw ExceptionUtilities.UnexpectedValue(accessorToBlame)
            };

            // TODO Warning
            // diagnostics.Add(ErrorCode.WRN_AccessorDoesNotUseBackingField, accessorToBlame.GetFirstLocation(), accessorName, property);
        }
    }

    private void EnsureSignature() {
        if (!_state.HasComplete(CompletionParts.FinishPropertyEnsureSignature)) {
            lock (_syntaxRef) {
                if (_state.NotePartComplete(CompletionParts.StartPropertyEnsureSignature)) {
                    var diagnostics = BelteDiagnosticQueue.GetInstance();

                    try {
                        EnsureSignatureGuarded(diagnostics);
                        AddDeclarationDiagnostics(diagnostics);
                    } finally {
                        _state.NotePartComplete(CompletionParts.FinishPropertyEnsureSignature);
                        diagnostics.Free();
                    }
                }
            }
        }
    }

    internal bool CanUseBackingFieldDirectlyInConstructor(bool useAsLvalue) {
        if (backingField is null)
            return false;

        if (useAsLvalue)
            return setMethod is null || hasAutoPropertySet;
        else
            return getMethod is null || hasAutoPropertyGet;
    }

    private bool IsSetOnEitherPart(Flags flags) {
        return (_propertyFlags & flags) != 0;
    }

    internal void SetMergedBackingField(SynthesizedBackingFieldSymbol? backingField) {
        Interlocked.CompareExchange(
            ref _lazyMergedBackingField,
            new StrongBox<SynthesizedBackingFieldSymbol?>(backingField),
            null
        );

        Debug.Assert((object)_lazyMergedBackingField.Value == backingField);
    }

    private SynthesizedBackingFieldSymbol CreateBackingField() {
        var fieldName = GeneratedNames.MakeBackingFieldName(_name);

        bool isConst;

        if (hasConstModifier) {
            isConst = true;
        } else if ((_setMethod is null || _setMethod.isInitOnly || _setMethod.isDeclaredConst) &&
            (_getMethod is null || (_propertyFlags & Flags.HasAutoPropertyGet) != 0 || _getMethod.isDeclaredConst)) {
            isConst = true;
        } else {
            isConst = false;
        }

        return new SynthesizedBackingFieldSymbol(
            this,
            fieldName,
            isConst: isConst,
            isStatic: isStatic,
            hasInitializer: (_propertyFlags & Flags.HasInitializer) != 0
        );
    }

    internal override void AfterAddingTypeMembersChecks(ConversionsBase conversions, BelteDiagnosticQueue diagnostics) {
        var isExplicitInterfaceImplementation = this.isExplicitInterfaceImplementation;
        CheckAccessibility(location, diagnostics, isExplicitInterfaceImplementation);
        CheckModifiers(isExplicitInterfaceImplementation, location, diagnostics);

        CheckInitializerIfNeeded(diagnostics);

        if (isAutoPropertyOrUsesFieldKeyword) {
            if (!isStatic && hasAutoPropertySet/* && setMethod is { isInitOnly: false }*/) {
                if (hasConstModifier) {
                    // TODO
                    throw ExceptionUtilities.Unreachable();
                    // diagnostics.Add(ErrorCode.ERR_AutoPropertyWithSetterCantBeReadOnly, location, this);
                }
            }

            if (refKind != RefKind.None)
                diagnostics.Push(Error.AutoPropertyCannotBeRefReturning(location));

            if (isOverride) {
                var overriddenProperty = (PropertySymbol)GetLeastOverriddenMember(containingType);

                if ((overriddenProperty.getMethod is { } && getMethod is null) ||
                    (overriddenProperty.setMethod is { } && setMethod is null)) {
                    // TODO
                    throw ExceptionUtilities.Unreachable();
                    // diagnostics.Add(ErrorCode.ERR_AutoPropertyMustOverrideSet, location);
                }
            }
        }

        if (!isStatic &&
            containingType.isInterface &&
            IsSetOnEitherPart(Flags.RequiresBackingField) &&
            !IsSetOnEitherPart(Flags.HasInitializer)) {
            diagnostics.Push(Error.InterfacesCantContainFields(location));
        }

        if (!isExpressionBodied) {
            var hasGetAccessor = getMethod is not null;
            var hasSetAccessor = setMethod is not null;

            if (hasGetAccessor && hasSetAccessor) {
                Debug.Assert(_getMethod is not null);
                Debug.Assert(_setMethod is not null);

                if (_refKind != RefKind.None) {
                    diagnostics.Push(Error.RefPropertyCannotHaveSetAccessor(_setMethod.location));
                } else if ((_getMethod.localAccessibility != Accessibility.NotApplicable) &&
                      (_setMethod.localAccessibility != Accessibility.NotApplicable)) {
                    throw ExceptionUtilities.Unreachable();
                    // diagnostics.Add(ErrorCode.ERR_DuplicatePropertyAccessMods, location, this);
                } else if (_getMethod.localDeclaredConst && _setMethod.localDeclaredConst) {
                    throw ExceptionUtilities.Unreachable();
                    // diagnostics.Add(ErrorCode.ERR_DuplicatePropertyReadOnlyMods, location, this);
                } else if (isAbstract) {
                    CheckAbstractPropertyAccessorNotPrivate(_getMethod, diagnostics);
                    CheckAbstractPropertyAccessorNotPrivate(_setMethod, diagnostics);
                }
            } else {
                if (!hasGetAccessor && !hasSetAccessor) {
                    diagnostics.Push(Error.PropertyWithNoAccessors(location, this));
                } else if (refKind != RefKind.None) {
                    if (!hasGetAccessor)
                        diagnostics.Push(Error.RefPropertyMustHaveGetAccessor(location));
                } else if (!hasGetAccessor && hasAutoPropertySet) {
                    // TODO
                    throw ExceptionUtilities.Unreachable();
                    // diagnostics.Add(ErrorCode.ERR_AutoPropertyMustHaveGetAccessor, _setMethod!.GetFirstlocation());
                }

                if (!isOverride) {
                    var accessor = _getMethod ?? _setMethod;

                    if (accessor is not null) {
                        if (accessor.localAccessibility != Accessibility.NotApplicable) {
                            // TODO
                            throw ExceptionUtilities.Unreachable();
                            // diagnostics.Add(ErrorCode.ERR_AccessModMissingAccessor, location, this);
                        }

                        if (accessor.localDeclaredConst) {
                            // TODO
                            throw ExceptionUtilities.Unreachable();
                            // diagnostics.Add(ErrorCode.ERR_ReadOnlyModMissingAccessor, location, this);
                        }
                    }
                }
            }

            CheckAccessibilityMoreRestrictive(_getMethod, diagnostics);
            CheckAccessibilityMoreRestrictive(_setMethod, diagnostics);
        }

        var explicitlyImplementedProperty = explicitInterfaceImplementations.FirstOrDefault();

        if (explicitlyImplementedProperty is not null) {
            CheckExplicitImplementationAccessor(
                getMethod,
                explicitlyImplementedProperty.getMethod,
                explicitlyImplementedProperty,
                diagnostics
            );

            CheckExplicitImplementationAccessor(
                setMethod,
                explicitlyImplementedProperty.setMethod,
                explicitlyImplementedProperty,
                diagnostics
            );
        }

        var typeLocation = _typeLocation;
        var compilation = declaringCompilation;

        Debug.Assert(typeLocation is not null);

        if (_explicitInterfaceType is not null) {
            var explicitInterfaceSpecifier = GetExplicitInterfaceSpecifier();
            var impliedConstraints = GetEnclosingTemplateConstraints();
            Debug.Assert(explicitInterfaceSpecifier is not null);

            _explicitInterfaceType.CheckAllConstraints(
                // compilation,
                conversions,
                explicitInterfaceSpecifier.name.location,
                impliedConstraints,
                diagnostics
            );

            if (explicitlyImplementedProperty is not null) {
                TypeSymbol.CheckModifierMismatchOnImplementingMember(
                    containingType,
                    this,
                    explicitlyImplementedProperty,
                    isExplicit: true,
                    diagnostics
                );
            }
        }
    }

    private void CheckAccessibility(
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        bool isExplicitInterfaceImplementation) {
        ModifierHelpers.CheckAccessibility(
            _modifiers,
            // this,
            // isExplicitInterfaceImplementation,
            diagnostics,
            location
        );
    }

    private void CheckModifiers(
        bool isExplicitInterfaceImplementation,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        Debug.Assert(!isStatic || !isOverride);
        Debug.Assert(!isStatic || containingType.isInterface || (!isAbstract && !isVirtual));

        var isExplicitInterfaceImplementationInInterface = isExplicitInterfaceImplementation &&
            containingType.isInterface;

        if (declaredAccessibility == Accessibility.Private &&
            (isVirtual || (isAbstract && !isExplicitInterfaceImplementationInInterface) || isOverride)) {
            // TODO Specialized error?
            diagnostics.Push(Error.CannotBePrivateAndVirtualOrAbstract(location, this));
        } else if (isStatic && hasConstModifier) {
            diagnostics.Push(Error.StaticAndConst(location, this));
        } else if (isOverride && (isNew || isVirtual)) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_OverrideNotNew, location, this);
        } else if (isSealed && !isOverride && !(isAbstract && isExplicitInterfaceImplementationInInterface)) {
            // '{0}' cannot be sealed because it is not an override
            diagnostics.Push(Error.SealedNonOverride(location, this));
        } else if (isAbstract && containingType.typeKind == TypeKind.Struct) {
            diagnostics.Push(Error.InvalidModifier(location, SyntaxFacts.GetText(SyntaxKind.AbstractKeyword)));
        } else if (isVirtual && containingType.typeKind == TypeKind.Struct) {
            diagnostics.Push(Error.InvalidModifier(location, SyntaxFacts.GetText(SyntaxKind.VirtualKeyword)));
        } else if (isAbstract && isExtern) {
            diagnostics.Push(Error.AbstractAndExtern(location, this));
        } else if (isAbstract && isSealed && !isExplicitInterfaceImplementationInInterface) {
            diagnostics.Push(Error.AbstractAndSealed(location, this));
        } else if (isAbstract && isVirtual) {
            diagnostics.Push(Error.AbstractAndVirtual(location, kind.Localize(), this));
        } else if (containingType.isSealed && declaredAccessibility.HasProtected() && !isOverride) {
            diagnostics.Push(AccessCheck.GetProtectedMemberInSealedTypeError(containingType, location));
        } else if (containingType.isStatic && !isStatic) {
            diagnostics.Push(Error.InstanceMemberInStatic(location, this));
        }
    }

    private void CheckAccessibilityMoreRestrictive(
        SourcePropertyAccessorSymbol accessor,
        BelteDiagnosticQueue diagnostics) {
        if (accessor is not null &&
            !IsAccessibilityMoreRestrictive(declaredAccessibility, accessor.localAccessibility)) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_InvalidPropertyAccessMod, accessor.GetFirstLocation(), accessor, this);
        }
    }

    private static bool IsAccessibilityMoreRestrictive(Accessibility property, Accessibility accessor) {
        if (accessor == Accessibility.NotApplicable)
            return true;

        return (accessor < property) &&
            ((accessor != Accessibility.Protected) || (property != Accessibility.Internal));
    }

    private static void CheckAbstractPropertyAccessorNotPrivate(
        SourcePropertyAccessorSymbol accessor,
        BelteDiagnosticQueue diagnostics) {
        if (accessor.localAccessibility == Accessibility.Private) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_PrivateAbstractAccessor, accessor.GetFirstLocation(), accessor);
        }
    }

    private void CheckExplicitImplementationAccessor(
        MethodSymbol thisAccessor,
        MethodSymbol otherAccessor,
        PropertySymbol explicitlyImplementedProperty,
        BelteDiagnosticQueue diagnostics) {
        var thisHasAccessor = thisAccessor is not null;
        var otherHasAccessor = otherAccessor.IsImplementable();

        if (otherHasAccessor && !thisHasAccessor) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_ExplicitPropertyMissingAccessor, this.Location, this, otherAccessor);
        } else if (!otherHasAccessor && thisHasAccessor) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_ExplicitPropertyAddingAccessor, thisAccessor.GetFirstLocation(), thisAccessor, explicitlyImplementedProperty);
        }
    }

    private SynthesizedSealedPropertyAccessor MakeSynthesizedSealedAccessor() {
        Debug.Assert(isSealed && (getMethod is null || setMethod is null));

        if (getMethod is not null) {
            var overriddenAccessor = GetOwnOrInheritedSetMethod();
            return overriddenAccessor is null ? null : new SynthesizedSealedPropertyAccessor(this, overriddenAccessor);
        } else if (setMethod is not null) {
            var overriddenAccessor = GetOwnOrInheritedGetMethod();
            return overriddenAccessor is null ? null : new SynthesizedSealedPropertyAccessor(this, overriddenAccessor);
        } else {
            return null;
        }
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyCustomAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        var copyFrom = _boundAttributesSource;

        Debug.Assert(!ReferenceEquals(copyFrom, this));

        _ = backingField?.GetAttributes();

        bool bagCreatedOnThisThread;

        if (copyFrom is not null) {
            var attributesBag = copyFrom.GetAttributesBag();
            bagCreatedOnThisThread = Interlocked.CompareExchange(
                ref _lazyCustomAttributesBag,
                attributesBag,
                null
            ) is null;
        } else {
            bagCreatedOnThisThread = LoadAndValidateAttributes(
                GetAttributeDeclarations(),
                ref _lazyCustomAttributesBag
            );
        }

        if (bagCreatedOnThisThread) {
            var completed = _state.NotePartComplete(CompletionParts.Attributes);
            Debug.Assert(completed);
        }

        Debug.Assert(_lazyCustomAttributesBag.isSealed);
        return _lazyCustomAttributesBag;
    }

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    private PropertyWellKnownAttributeData GetDecodedWellKnownAttributeData() {
        var attributesBag = _lazyCustomAttributesBag;

        if (attributesBag is null || !attributesBag.isDecodedWellKnownAttributeDataComputed)
            attributesBag = GetAttributesBag();

        return (PropertyWellKnownAttributeData)attributesBag.decodedWellKnownAttributeData;
    }

    internal PropertyEarlyWellKnownAttributeData GetEarlyDecodedWellKnownAttributeData() {
        var attributesBag = _lazyCustomAttributesBag;

        if (attributesBag is null || !attributesBag.isEarlyDecodedWellKnownAttributeDataComputed)
            attributesBag = GetAttributesBag();

        return (PropertyEarlyWellKnownAttributeData)attributesBag.earlyDecodedWellKnownAttributeData;
    }

    internal override (AttributeData, BoundAttribute) EarlyDecodeWellKnownAttribute(
        ref EarlyDecodeWellKnownAttributeArguments<EarlyWellKnownAttributeBinder, NamedTypeSymbol, AttributeSyntax, AttributeLocation> arguments) {
        // TODO Stuff
        return base.EarlyDecodeWellKnownAttribute(ref arguments);
    }

    private protected override void DecodeWellKnownAttributeImpl(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        Debug.Assert(arguments.attributeSyntax is not null);

        var diagnostics = arguments.diagnostics;
        var attribute = arguments.attribute;

        Debug.Assert(!attribute.hasErrors);
        Debug.Assert(arguments.symbolPart == AttributeLocation.None);
        // TODO Stuff
    }

    private SourceAttributeData FindAttribute(AttributeDescription attributeDescription) {
        return (SourceAttributeData)GetAttributes().First(a => a.IsTargetAttribute(attributeDescription));
    }

    private ImmutableArray<SourceAttributeData> FindAttributes(AttributeDescription attributeDescription) {
        return GetAttributes()
            .Where(a => a.IsTargetAttribute(attributeDescription))
            .Cast<SourceAttributeData>()
            .ToImmutableArray();
    }

    internal override void PostDecodeWellKnownAttributes(
        ImmutableArray<AttributeData> boundAttributes,
        ImmutableArray<AttributeSyntax> allAttributeSyntaxNodes,
        BelteDiagnosticQueue diagnostics,
        AttributeLocation symbolPart,
        WellKnownAttributeData decodedData) {
        Debug.Assert(!boundAttributes.IsDefault);
        Debug.Assert(!allAttributeSyntaxNodes.IsDefault);
        Debug.Assert(boundAttributes.Length == allAttributeSyntaxNodes.Length);
        Debug.Assert(_lazyCustomAttributesBag is not null);
        Debug.Assert(_lazyCustomAttributesBag.isDecodedWellKnownAttributeDataComputed);
        Debug.Assert(symbolPart == AttributeLocation.None);

        base.PostDecodeWellKnownAttributes(
            boundAttributes,
            allAttributeSyntaxNodes,
            diagnostics,
            symbolPart,
            decodedData
        );
    }

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Attributes:
                    GetAttributes();
                    break;
                case CompletionParts.StartPropertyEnsureSignature:
                case CompletionParts.FinishPropertyEnsureSignature:
                    EnsureSignature();
                    Debug.Assert(_state.HasComplete(CompletionParts.FinishPropertyEnsureSignature));
                    break;
                case CompletionParts.StartPropertyParameters:
                case CompletionParts.FinishPropertyParameters: {
                        if (_state.NotePartComplete(CompletionParts.StartPropertyParameters)) {
                            var parameters = this.parameters;

                            if (parameters.Length > 0) {
                                var diagnostics = BelteDiagnosticQueue.GetInstance();
                                var conversions = TypeConversions.GetInstance();
                                var impliedConstraints = GetEnclosingTemplateConstraints();

                                foreach (var parameter in this.parameters) {
                                    parameter.ForceComplete(location);
                                    parameter.type.CheckAllConstraints(
                                        conversions,
                                        parameter.location,
                                        impliedConstraints,
                                        diagnostics
                                    );
                                }

                                AddDeclarationDiagnostics(diagnostics);
                                diagnostics.Free();
                            }

                            var completedOnThisThread = _state.NotePartComplete(
                                CompletionParts.FinishPropertyParameters
                            );

                            Debug.Assert(completedOnThisThread);
                        } else {
                            _state.SpinWaitComplete(CompletionParts.FinishPropertyParameters);
                        }
                    }

                    break;
                case CompletionParts.StartPropertyType:
                case CompletionParts.FinishPropertyType: {
                        if (_state.NotePartComplete(CompletionParts.StartPropertyType)) {
                            var diagnostics = BelteDiagnosticQueue.GetInstance();
                            var conversions = TypeConversions.GetInstance();
                            var impliedConstraints = GetEnclosingTemplateConstraints();
                            type.CheckAllConstraints(conversions, location, impliedConstraints, diagnostics);

                            ValidatePropertyType(diagnostics);

                            AddDeclarationDiagnostics(diagnostics);
                            var completedOnThisThread = _state.NotePartComplete(CompletionParts.FinishPropertyType);
                            Debug.Assert(completedOnThisThread);
                            diagnostics.Free();
                        } else {
                            _state.SpinWaitComplete(CompletionParts.FinishPropertyType);
                        }
                    }

                    break;
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.PropertySymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }
    }

    private protected virtual void ValidatePropertyType(BelteDiagnosticQueue diagnostics) {
        var type = this.type;

        if (type.isStatic) {
            if (getMethod is not null)
                diagnostics.Push(Error.CannotReturnStatic(_typeLocation, type));
            else if (setMethod is not null)
                diagnostics.Push(Error.ParameterIsStatic(_typeLocation, type));
        }
    }

    private protected abstract (TypeWithAnnotations Type, ImmutableArray<ParameterSymbol> Parameters) MakeParametersAndBindType(
        BelteDiagnosticQueue diagnostics);

    private protected static ExplicitInterfaceSpecifierSyntax GetExplicitInterfaceSpecifier(SyntaxNode syntax)
        => (syntax as PropertyDeclarationSyntax)?.explicitInterfaceSpecifier;

    internal ExplicitInterfaceSpecifierSyntax GetExplicitInterfaceSpecifier() {
        return GetExplicitInterfaceSpecifier(belteSyntaxNode);
    }

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => attributesOwner;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.Property;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations
        => isAutoPropertyOrUsesFieldKeyword
            ? AttributeLocation.Property | AttributeLocation.Field
            : AttributeLocation.Property;
}
