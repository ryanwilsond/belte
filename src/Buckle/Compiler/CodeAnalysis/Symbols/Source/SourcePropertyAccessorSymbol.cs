using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal class SourcePropertyAccessorSymbol : SourceMemberMethodSymbol {
    private readonly SourcePropertySymbolBase _property;
    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TypeWithAnnotations _lazyReturnType;
    private ImmutableArray<MethodSymbol> _lazyExplicitInterfaceImplementations;
    private string _lazyName;
    private readonly bool _isAutoPropertyAccessor;
    private readonly bool _usesInit;

    private SourcePropertyAccessorSymbol(
        NamedTypeSymbol containingType,
        SourcePropertySymbol property,
        DeclarationModifiers propertyModifiers,
        TextLocation location,
        ArrowExpressionClauseSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            location,
            MakeModifiersAndFlags(
                containingType,
                property,
                propertyModifiers,
                location,
                hasBlockBody: false,
                hasExpressionBody: true,
                modifiers: SyntaxTokenList.Empty,
                methodKind: MethodKind.PropertyGet,
                diagnostics,
                out var modifierErrors)
            ) {
        _property = property;
        _isAutoPropertyAccessor = false;

        CheckModifiersForBody(location, diagnostics);

        ModifierHelpers.CheckAccessibility(
            _modifiers,
            // this,
            // property.IsExplicitInterfaceImplementation,
            diagnostics,
            location
        );

        CheckModifiers(location, hasBody: true, isAutoPropertyOrExpressionBodied: true, diagnostics: diagnostics);
    }

    private protected SourcePropertyAccessorSymbol(
        NamedTypeSymbol containingType,
        SourcePropertySymbolBase property,
        DeclarationModifiers propertyModifiers,
        TextLocation location,
        BelteSyntaxNode syntax,
        bool hasBlockBody,
        bool hasExpressionBody,
        bool isIterator,
        SyntaxTokenList modifiers,
        MethodKind methodKind,
        bool usesInit,
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            location,
            // isIterator,
            MakeModifiersAndFlags(
                containingType,
                property,
                propertyModifiers,
                location,
                hasBlockBody,
                hasExpressionBody,
                modifiers,
                methodKind,
                diagnostics,
                out var modifierErrors
            )) {
        _property = property;
        _isAutoPropertyAccessor = isAutoPropertyAccessor;
        var hasAnyBody = hasBlockBody || hasExpressionBody;
        _usesInit = usesInit;

        if (hasAnyBody)
            CheckModifiersForBody(location, diagnostics);

        ModifierHelpers.CheckAccessibility(
            _modifiers,
            // this,
            // property.IsExplicitInterfaceImplementation,
            diagnostics,
            location
        );

        if (!modifierErrors)
            CheckModifiers(location, hasAnyBody, isAutoPropertyAccessor, diagnostics);
    }

    internal sealed override Accessibility declaredAccessibility {
        get {
            var accessibility = localAccessibility;

            if (accessibility != Accessibility.NotApplicable)
                return accessibility;

            var propertyAccessibility = _property.declaredAccessibility;
            Debug.Assert(propertyAccessibility != Accessibility.NotApplicable);
            return propertyAccessibility;
        }
    }

    public sealed override Symbol associatedSymbol => _property;

    public sealed override bool returnsVoid => returnType.IsVoidType();

    internal sealed override ImmutableArray<ParameterSymbol> parameters {
        get {
            LazyMethodChecks();
            return _lazyParameters;
        }
    }

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes()
        => [];

    internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() => [];

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            LazyMethodChecks();
            return _lazyReturnType;
        }
    }

    internal Accessibility localAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal bool localDeclaredConst => (_modifiers & DeclarationModifiers.Const) != 0;

    internal bool localDeclaredLowLevel => (_modifiers & DeclarationModifiers.LowLevel) != 0;

    internal override bool isLowLevel => localDeclaredLowLevel || _property.hasLowLevelModifier;

    internal sealed override bool isDeclaredConst {
        get {
            if (localDeclaredConst || _property.hasConstModifier)
                return true;

            // TODO Some PEVerify related implicit readonly issues need addressing
            return false;
        }
    }

    internal bool isAutoPropertyAccessor => _isAutoPropertyAccessor;

    // internal sealed override bool isInitOnly => !isStatic && _usesInit;
    internal bool isInitOnly => !isStatic && _usesInit;

    internal sealed override bool isExplicitInterfaceImplementation => _property.isExplicitInterfaceImplementation;

    internal sealed override ImmutableArray<MethodSymbol> explicitInterfaceImplementations {
        get {
            if (_lazyExplicitInterfaceImplementations.IsDefault) {
                var explicitlyImplementedPropertyOpt = isExplicitInterfaceImplementation
                    ? _property.explicitInterfaceImplementations.FirstOrDefault()
                    : null;

                ImmutableArray<MethodSymbol> explicitInterfaceImplementations;

                if (explicitlyImplementedPropertyOpt is null) {
                    explicitInterfaceImplementations = [];
                } else {
                    var implementedAccessor = methodKind == MethodKind.PropertyGet
                        ? explicitlyImplementedPropertyOpt.getMethod
                        : explicitlyImplementedPropertyOpt.setMethod;

                    explicitInterfaceImplementations = implementedAccessor is null
                        ? []
                        : [implementedAccessor];
                }

                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyExplicitInterfaceImplementations,
                    explicitInterfaceImplementations
                );
            }

            return _lazyExplicitInterfaceImplementations;
        }
    }

    public sealed override string name {
        get {
            if (_lazyName is null) {
                var isGetMethod = methodKind == MethodKind.PropertyGet;
                string name = null;

                if (isExplicitInterfaceImplementation) {
                    var explicitlyImplementedPropertyOpt = _property.explicitInterfaceImplementations.FirstOrDefault();

                    if (explicitlyImplementedPropertyOpt is not null) {
                        var implementedAccessor = isGetMethod
                            ? explicitlyImplementedPropertyOpt.getMethod
                            : explicitlyImplementedPropertyOpt.setMethod;

                        var accessorName = implementedAccessor is not null
                            ? implementedAccessor.name
                            : GetAccessorName(explicitlyImplementedPropertyOpt.metadataName, isGetMethod);

                        var aliasQualifierOpt = _property.GetExplicitInterfaceSpecifier()?.name.GetAliasQualifier();
                        name = ExplicitInterfaceHelpers.GetMemberName(
                            accessorName,
                            explicitlyImplementedPropertyOpt.containingType,
                            aliasQualifierOpt
                        );
                    }
                } else if (isOverride) {
                    var overriddenMethod = this.overriddenMethod;

                    if (overriddenMethod is not null)
                        name = overriddenMethod.name;
                }

                name ??= GetAccessorName(_property.sourceName, isGetMethod);

                InterlockedOperations.Initialize(ref _lazyName, name);
            }

            return _lazyName;
        }
    }

    internal override bool isImplicitlyDeclared {
        get {
            switch (GetSyntax().kind) {
                case SyntaxKind.AccessorDeclaration:
                case SyntaxKind.ArrowExpressionClause:
                    return false;
            }

            return true;
        }
    }

    internal static SourcePropertyAccessorSymbol CreateAccessorSymbol(
        NamedTypeSymbol containingType,
        SourcePropertySymbol property,
        DeclarationModifiers propertyModifiers,
        AccessorDeclarationSyntax syntax,
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics) {
        var isGetMethod = syntax.keyword.kind == SyntaxKind.GetKeyword;
        var methodKind = isGetMethod ? MethodKind.PropertyGet : MethodKind.PropertySet;

        var hasBody = syntax.body is not null;
        var hasExpressionBody = syntax.expressionBody is not null;
        // CheckForBlockAndExpressionBody(syntax.Body, syntax.ExpressionBody, syntax, diagnostics);

        return new SourcePropertyAccessorSymbol(
            containingType,
            property,
            propertyModifiers,
            syntax.keyword.location,
            syntax,
            hasBody,
            hasExpressionBody,
            isIterator: false,
            syntax.modifiers,
            methodKind,
            false,
            isAutoPropertyAccessor,
            diagnostics
        );
    }

    internal static SourcePropertyAccessorSymbol CreateAccessorSymbol(
        NamedTypeSymbol containingType,
        SourcePropertySymbol property,
        DeclarationModifiers propertyModifiers,
        ArrowExpressionClauseSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        return new SourcePropertyAccessorSymbol(
            containingType,
            property,
            propertyModifiers,
            syntax.expression.location,
            syntax,
            diagnostics
        );
    }

    internal override ImmutableArray<BoundExpression> GetTemplateConstraints() {
        return [];
    }

    private TypeWithAnnotations ComputeReturnType(BelteDiagnosticQueue diagnostics) {
        if (methodKind == MethodKind.PropertyGet) {
            return _property.typeWithAnnotations;
        } else {
            var binder = GetBinder();
            var type = new TypeWithAnnotations(binder.compilation.GetSpecialType(SpecialType.Void));

            if (isInitOnly) {
                // TODO PE
                // var isInitOnlyType = Binder.GetWellKnownType(this.DeclaringCompilation,
                //     WellKnownType.System_Runtime_CompilerServices_IsExternalInit, diagnostics, _location);

                // var modifiers = ImmutableArray.Create<CustomModifier>(
                //     CSharpCustomModifier.CreateRequired(isInitOnlyType));
                // type = type.WithModifiers(modifiers);
            }

            return type;
        }
    }

    private Binder GetBinder() {
        var syntax = GetSyntax();
        var compilation = declaringCompilation;
        var binderFactory = compilation.GetBinderFactory(syntax.syntaxTree);
        return binderFactory.GetBinder(syntax);
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        NamedTypeSymbol containingType,
        SourcePropertySymbolBase property,
        DeclarationModifiers propertyModifiers,
        TextLocation location,
        bool hasBlockBody,
        bool hasExpressionBody,
        SyntaxTokenList modifiers,
        MethodKind methodKind,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var isExpressionBodied = !hasBlockBody && hasExpressionBody;
        var hasAnyBody = hasBlockBody || hasExpressionBody;

        var isExplicitInterfaceImplementation = property.isExplicitInterfaceImplementation;
        var declarationModifiers = MakeModifiers(
            containingType,
            modifiers,
            isExplicitInterfaceImplementation,
            hasAnyBody,
            location,
            diagnostics,
            out modifierErrors
        );

        declarationModifiers |= GetAccessorModifiers(propertyModifiers) & ~DeclarationModifiers.AccessibilityMask;

        if ((declarationModifiers & DeclarationModifiers.Private) != 0)
            declarationModifiers &= ~DeclarationModifiers.Virtual;

        var flags = MakeFlags(
            methodKind,
            property.refKind,
            declarationModifiers,
            returnsVoid: false,
            returnsVoidIsSet: false,
            hasAnyBody: isExpressionBodied || hasBlockBody,
            // isExtensionMethod: false,
            // isVarArg: false,
            // isExplicitInterfaceImplementation: isExplicitInterfaceImplementation,
            hasThisInitializer: false
        );

        return (declarationModifiers, flags);
    }

    private static DeclarationModifiers GetAccessorModifiers(DeclarationModifiers propertyModifiers) {
        return propertyModifiers & ~(DeclarationModifiers.Const | DeclarationModifiers.LowLevel);
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactoryOpt = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactoryOpt, ignoreAccessibility);
    }

    private protected sealed override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        _lazyParameters = ComputeParameters();
        _lazyReturnType = ComputeReturnType(diagnostics);

        var explicitInterfaceImplementations = this.explicitInterfaceImplementations;

        if (explicitInterfaceImplementations.Length > 0) {
            Debug.Assert(explicitInterfaceImplementations.Length == 1);
            var implementedMethod = explicitInterfaceImplementations[0];
            _lazyParameters = implementedMethod.parameters;
            // CustomModifierUtils.CopyMethodCustomModifiers(implementedMethod, this, out _lazyReturnType,
            //                                               out _lazyRefCustomModifiers,
            //                                               out _lazyParameters, alsoCopyParamsModifier: false);
        } else if (isOverride) {
            var overriddenMethod = this.overriddenMethod;

            if (overriddenMethod is not null) {
                _lazyParameters = overriddenMethod.parameters;
                // CustomModifierUtils.CopyMethodCustomModifiers(overriddenMethod, this, out _lazyReturnType,
                //                                               out _lazyRefCustomModifiers,
                //                                               out _lazyParameters, alsoCopyParamsModifier: true);
            }
        } else if (!_lazyReturnType.IsVoidType()) {
            PropertySymbol associatedProperty = _property;
            var type = associatedProperty.typeWithAnnotations;
            // _lazyReturnType = _lazyReturnType.WithTypeAndModifiers(
            //     CustomModifierUtils.CopyTypeCustomModifiers(type.Type, _lazyReturnType.Type, this.ContainingAssembly),
            //     type.CustomModifiers);
        }
    }

    private static DeclarationModifiers MakeModifiers(
        NamedTypeSymbol containingType,
        SyntaxTokenList modifiers,
        bool isExplicitInterfaceImplementation,
        bool hasBody,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        const DeclarationModifiers defaultAccess = DeclarationModifiers.None;

        var allowedModifiers = isExplicitInterfaceImplementation
            ? DeclarationModifiers.None
            : DeclarationModifiers.AccessibilityMask;

        allowedModifiers |= DeclarationModifiers.LowLevel | DeclarationModifiers.Const;

        var defaultInterfaceImplementationModifiers = DeclarationModifiers.None;
        var isInterface = containingType.isInterface;

        if (isInterface && !isExplicitInterfaceImplementation)
            defaultInterfaceImplementationModifiers = DeclarationModifiers.AccessibilityMask;

        var mods = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            // isOrdinaryMethod: false,
            modifiers,
            isForInterfaceMember: isInterface,
            defaultAccess,
            allowedModifiers,
            location,
            diagnostics,
            out modifierErrors
        );

        // TODO Do we care?
        // ModifierHelpers.ReportDefaultInterfaceImplementationModifiers(hasBody, mods,
        //                                                             defaultInterfaceImplementationModifiers,
        //                                                             location, diagnostics);

        return mods;
    }

    private void CheckModifiers(
        TextLocation location,
        bool hasBody,
        bool isAutoPropertyOrExpressionBodied,
        BelteDiagnosticQueue diagnostics) {
        var localAccessibility = this.localAccessibility;

        if (isAbstract && !containingType.isAbstract && containingType.typeKind == TypeKind.Class) {
            diagnostics.Push(Error.AbstractInNonAbstractType(location, this, containingType));
        } else if (isVirtual && containingType.isSealed && containingType.typeKind != TypeKind.Struct) {
            diagnostics.Push(Error.VirtualInSealedType(location, this, containingType));
        } else if (!hasBody && !isExtern && !isAbstract && !isAutoPropertyOrExpressionBodied) {
            diagnostics.Push(Error.NonAbstractMustHaveBody(location, this));
        } else if (containingType.isSealed && localAccessibility.HasProtected() && !isOverride) {
            diagnostics.Push(AccessCheck.GetProtectedMemberInSealedTypeError(containingType, location));
        } else if (localDeclaredConst && _property.hasConstModifier) {
            throw ExceptionUtilities.Unreachable();
            // // Cannot specify 'readonly' modifiers on both property or indexer '{0}' and its accessors.
            // diagnostics.Add(ErrorCode.ERR_InvalidPropertyReadOnlyMods, location, _property);
        } else if (localDeclaredConst && isStatic) {
            diagnostics.Push(Error.StaticAndConst(location, this));
        } else if (localDeclaredConst && isInitOnly) {
            throw ExceptionUtilities.Unreachable();
            // // 'init' accessors cannot be marked 'readonly'. Mark '{0}' readonly instead.
            // diagnostics.Add(ErrorCode.ERR_InitCannotBeReadonly, location, _property);
        } else if (localDeclaredConst && _isAutoPropertyAccessor && methodKind == MethodKind.PropertySet) {
            throw ExceptionUtilities.Unreachable();
            // // Auto-implemented accessor '{0}' cannot be marked 'readonly'.
            // diagnostics.Add(ErrorCode.ERR_AutoSetterCantBeReadOnly, location, this);
        } else if (_usesInit && isStatic) {
            throw ExceptionUtilities.Unreachable();
            // // The 'init' accessor is not valid on static members
            // diagnostics.Add(ErrorCode.ERR_BadInitAccessor, location);
        }
    }

    internal static string GetAccessorName(string propertyName, bool getNotSet) {
        var prefix = getNotSet ? "get_" : "set_";
        return prefix + propertyName;
    }

    internal BelteSyntaxNode GetSyntax() {
        Debug.Assert(syntaxReference is not null);
        return (BelteSyntaxNode)syntaxReference.node;
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(_attributeDeclarationList);
    }

    private SyntaxList<AttributeListSyntax> _attributeDeclarationList {
        get {
            if (_property.containingType is SourceMemberContainerTypeSymbol { anyMemberHasAttributes: true }) {
                var syntax = GetSyntax();

                switch (syntax.kind) {
                    case SyntaxKind.AccessorDeclaration:
                        return ((AccessorDeclarationSyntax)syntax).attributeLists;
                }
            }

            return default;
        }
    }

    private ImmutableArray<ParameterSymbol> ComputeParameters() {
        var isGetMethod = methodKind == MethodKind.PropertyGet;
        var propertyParameters = _property.parameters;
        var nPropertyParameters = propertyParameters.Length;
        var nParameters = nPropertyParameters + (isGetMethod ? 0 : 1);

        if (nParameters == 0)
            return [];

        var parameters = ArrayBuilder<ParameterSymbol>.GetInstance(nParameters);

        foreach (SourceParameterSymbol propertyParam in propertyParameters)
            parameters.Add(new SourcePropertyClonedParameterSymbolForAccessors(propertyParam, this));

        if (!isGetMethod)
            parameters.Add(new SynthesizedPropertyAccessorValueParameterSymbol(this, parameters.Count));

        return parameters.ToImmutableAndFree();
    }
}
