using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourcePropertySymbol : SourcePropertySymbolBase {
    private SourcePropertySymbol(
        SourceMemberContainerTypeSymbol containingType,
        PropertyDeclarationSyntax syntax,
        bool hasGetAccessor,
        bool hasSetAccessor,
        bool isExplicitInterfaceImplementation,
        TypeSymbol explicitInterfaceType,
        string aliasQualifier,
        DeclarationModifiers modifiers,
        bool hasExplicitAccessMod,
        bool hasAutoPropertyGet,
        bool hasAutoPropertySet,
        bool isExpressionBodied,
        bool accessorsHaveImplementation,
        bool getterUsesFieldKeyword,
        bool setterUsesFieldKeyword,
        string memberName,
        TextLocation location,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            syntax,
            hasGetAccessor: hasGetAccessor,
            hasSetAccessor: hasSetAccessor,
            isExplicitInterfaceImplementation,
            explicitInterfaceType,
            aliasQualifier,
            modifiers,
            hasInitializer: false,
            hasExplicitAccessMod: hasExplicitAccessMod,
            hasAutoPropertyGet: hasAutoPropertyGet,
            hasAutoPropertySet: hasAutoPropertySet,
            isExpressionBodied: isExpressionBodied,
            accessorsHaveImplementation: accessorsHaveImplementation,
            getterUsesFieldKeyword: getterUsesFieldKeyword,
            setterUsesFieldKeyword: setterUsesFieldKeyword,
            syntax.type.GetRefKind(),
            memberName,
            syntax.attributeLists,
            location,
            diagnostics) {
        // TODO Reachable? Parser should cover this
        // CheckForBlockAndExpressionBody(
        //     syntax.accessorList,
        //     syntax.GetExpressionBodySyntax(),
        //     syntax,
        //     diagnostics);
    }

    private protected override TextLocation _typeLocation => GetTypeSyntax(belteSyntaxNode).location;

    private SyntaxList<AttributeListSyntax> _attributeDeclarationSyntaxList {
        get {
            if (containingType is SourceMemberContainerTypeSymbol { anyMemberHasAttributes: true })
                return ((PropertyDeclarationSyntax)belteSyntaxNode).attributeLists;

            return default;
        }
    }

    private protected override SourcePropertySymbolBase _boundAttributesSource => null;

    internal override IAttributeTargetSymbol attributesOwner => this;

    internal sealed override bool isExtern => _hasExternModifier;

    internal static SourcePropertySymbol Create(
        SourceMemberContainerTypeSymbol containingType,
        Binder bodyBinder,
        PropertyDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var nameToken = syntax.identifier;
        var location = nameToken.location;
        return Create(containingType, bodyBinder, syntax, nameToken.valueText, location, diagnostics);
    }

    private static SourcePropertySymbol Create(
        SourceMemberContainerTypeSymbol containingType,
        Binder binder,
        PropertyDeclarationSyntax syntax,
        string name,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        GetAccessorDeclarations(
            syntax,
            diagnostics,
            out var isExpressionBodied,
            out var hasGetAccessorImplementation,
            out var hasSetAccessorImplementation,
            out var getterUsesFieldKeyword,
            out var setterUsesFieldKeyword,
            out var getSyntax,
            out var setSyntax
        );

        var accessorsHaveImplementation = hasGetAccessorImplementation || hasSetAccessorImplementation;

        var explicitInterfaceSpecifier = GetExplicitInterfaceSpecifier(syntax);
        var modifiersTokenList = GetModifierTokensSyntax(syntax);
        var isExplicitInterfaceImplementation = explicitInterfaceSpecifier is not null;

        var (modifiers, hasExplicitAccessMod) = MakeModifiers(
            containingType,
            modifiersTokenList,
            isExplicitInterfaceImplementation,
            isIndexer: false,
            accessorsHaveImplementation: accessorsHaveImplementation,
            location,
            diagnostics,
            out _
        );

        var allowAutoPropertyAccessors = (modifiers & (DeclarationModifiers.Abstract | DeclarationModifiers.Extern)) == 0 &&
            (!containingType.isInterface || hasGetAccessorImplementation || hasSetAccessorImplementation || (modifiers & DeclarationModifiers.Static) != 0);
        var hasAutoPropertyGet = allowAutoPropertyAccessors && getSyntax is not null && !hasGetAccessorImplementation;
        var hasAutoPropertySet = allowAutoPropertyAccessors && setSyntax is not null && !hasSetAccessorImplementation;

        var memberName = ExplicitInterfaceHelpers.GetMemberNameAndInterfaceSymbol(
            binder,
            modifiersTokenList,
            explicitInterfaceSpecifier,
            name,
            diagnostics,
            out var explicitInterfaceType,
            out var aliasQualifierOpt
        );

        return new SourcePropertySymbol(
            containingType,
            syntax,
            hasGetAccessor: getSyntax is not null || isExpressionBodied,
            hasSetAccessor: setSyntax is not null,
            isExplicitInterfaceImplementation,
            explicitInterfaceType,
            aliasQualifierOpt,
            modifiers,
            hasExplicitAccessMod: hasExplicitAccessMod,
            hasAutoPropertyGet: hasAutoPropertyGet,
            hasAutoPropertySet: hasAutoPropertySet,
            isExpressionBodied: isExpressionBodied,
            accessorsHaveImplementation: accessorsHaveImplementation,
            getterUsesFieldKeyword: getterUsesFieldKeyword,
            setterUsesFieldKeyword: setterUsesFieldKeyword,
            memberName,
            location,
            diagnostics
        );
    }

    private TypeSyntax GetTypeSyntax(SyntaxNode syntax) {
        return ((PropertyDeclarationSyntax)syntax).type;
    }

    private static SyntaxTokenList GetModifierTokensSyntax(SyntaxNode syntax) {
        return ((PropertyDeclarationSyntax)syntax).modifiers;
    }

    private static ArrowExpressionClauseSyntax? GetArrowExpression(SyntaxNode syntax) {
        return syntax switch {
            PropertyDeclarationSyntax p => p.expressionBody,
            _ => throw ExceptionUtilities.UnexpectedValue(syntax.kind)
        };
    }

    internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(_attributeDeclarationSyntaxList);
    }

    private static void GetAccessorDeclarations(
        BelteSyntaxNode syntaxNode,
        BelteDiagnosticQueue diagnostics,
        out bool isExpressionBodied,
        out bool hasGetAccessorImplementation,
        out bool hasSetAccessorImplementation,
        out bool getterUsesFieldKeyword,
        out bool setterUsesFieldKeyword,
        out AccessorDeclarationSyntax getSyntax,
        out AccessorDeclarationSyntax setSyntax) {
        var syntax = (PropertyDeclarationSyntax)syntaxNode;
        isExpressionBodied = syntax.accessorList is null;
        getSyntax = null;
        setSyntax = null;

        if (!isExpressionBodied) {
            getterUsesFieldKeyword = false;
            setterUsesFieldKeyword = false;
            hasGetAccessorImplementation = false;
            hasSetAccessorImplementation = false;
            foreach (var accessor in syntax.accessorList!.accessors) {
                switch (accessor.keyword.kind) {
                    case SyntaxKind.GetKeyword:
                        if (getSyntax is null) {
                            getSyntax = accessor;
                            hasGetAccessorImplementation = HasImplementation(accessor);
                            getterUsesFieldKeyword = ContainsFieldExpressionInAccessor(accessor);
                        } else {
                            // TODO
                            throw ExceptionUtilities.Unreachable();
                            // diagnostics.Add(ErrorCode.ERR_DuplicateAccessor, accessor.Keyword.GetLocation());
                        }

                        break;
                    case SyntaxKind.SetKeyword:
                        if (setSyntax is null) {
                            setSyntax = accessor;
                            hasSetAccessorImplementation = HasImplementation(accessor);
                            setterUsesFieldKeyword = ContainsFieldExpressionInAccessor(accessor);
                        } else {
                            // TODO
                            throw ExceptionUtilities.Unreachable();
                            // diagnostics.Add(ErrorCode.ERR_DuplicateAccessor, accessor.Keyword.GetLocation());
                        }

                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(accessor.kind);
                }
            }
        } else {
            var body = GetArrowExpression(syntax);
            hasGetAccessorImplementation = body is not null;
            hasSetAccessorImplementation = false;
            getterUsesFieldKeyword = body is { } && ContainsFieldExpressionInGreenNode(body.green);
            setterUsesFieldKeyword = false;
            Debug.Assert(hasGetAccessorImplementation);
        }

        static bool HasImplementation(AccessorDeclarationSyntax accessor) {
            var body = (SyntaxNode?)accessor.body ?? accessor.expressionBody;
            return body is not null;
        }

        static bool ContainsFieldExpressionInAccessor(AccessorDeclarationSyntax syntax) {
            var accessorDeclaration = (Syntax.InternalSyntax.AccessorDeclarationSyntax)syntax.green;

            foreach (var attributeList in accessorDeclaration.attributeLists) {
                var attributes = attributeList.attributes;

                for (var i = 0; i < attributes.Count; i++) {
                    if (ContainsFieldExpressionInGreenNode(attributes[i]))
                        return true;
                }
            }
            return ContainsFieldExpressionInGreenNode(accessorDeclaration.body) ||
                ContainsFieldExpressionInGreenNode(accessorDeclaration.expressionBody);
        }

        static bool ContainsFieldExpressionInGreenNode(GreenNode green) {
            if (green is not null) {
                foreach (var node in green.EnumerateNodes()) {
                    if (node.kind == SyntaxKind.FieldExpression)
                        return true;
                }
            }

            return false;
        }
    }

    private static AccessorDeclarationSyntax GetGetAccessorDeclaration(PropertyDeclarationSyntax syntax) {
        foreach (var accessor in syntax.accessorList.accessors) {
            switch (accessor.keyword.kind) {
                case SyntaxKind.GetKeyword:
                    return accessor;
            }
        }

        throw ExceptionUtilities.Unreachable();
    }

    private static AccessorDeclarationSyntax GetSetAccessorDeclaration(PropertyDeclarationSyntax syntax) {
        foreach (var accessor in syntax.accessorList.accessors) {
            switch (accessor.keyword.kind) {
                case SyntaxKind.SetKeyword:
                    return accessor;
            }
        }

        throw ExceptionUtilities.Unreachable();
    }

    private static (DeclarationModifiers modifiers, bool hasExplicitAccessMod) MakeModifiers(
        NamedTypeSymbol containingType,
        SyntaxTokenList modifiers,
        bool isExplicitInterfaceImplementation,
        bool isIndexer,
        bool accessorsHaveImplementation,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        out bool modifierErrors) {
        var isInterface = containingType.isInterface;
        var defaultAccess = isInterface && !isExplicitInterfaceImplementation
            ? DeclarationModifiers.Public
            : DeclarationModifiers.Private;

        var allowedModifiers = DeclarationModifiers.LowLevel;
        var defaultInterfaceImplementationModifiers = DeclarationModifiers.None;

        if (!isExplicitInterfaceImplementation) {
            allowedModifiers |= DeclarationModifiers.AccessibilityMask;

            allowedModifiers |= DeclarationModifiers.New |
                                DeclarationModifiers.Sealed |
                                DeclarationModifiers.Abstract |
                                DeclarationModifiers.Virtual;

            if (!isIndexer)
                allowedModifiers |= DeclarationModifiers.Static;

            if (!isInterface) {
                allowedModifiers |= DeclarationModifiers.Override;
            } else {
                defaultAccess = DeclarationModifiers.None;

                defaultInterfaceImplementationModifiers |= DeclarationModifiers.Sealed |
                                                           DeclarationModifiers.Abstract |
                                                           (isIndexer ? 0 : DeclarationModifiers.Static) |
                                                           DeclarationModifiers.Virtual |
                                                           DeclarationModifiers.Extern |
                                                           DeclarationModifiers.AccessibilityMask;
            }
        } else {
            Debug.Assert(isExplicitInterfaceImplementation);

            if (isInterface)
                allowedModifiers |= DeclarationModifiers.Abstract;

            if (!isIndexer)
                allowedModifiers |= DeclarationModifiers.Static;
        }

        allowedModifiers |= DeclarationModifiers.Const;

        allowedModifiers |= DeclarationModifiers.Extern;

        var mods = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            modifiers,
            isForInterfaceMember: isInterface,
            defaultAccess,
            allowedModifiers,
            location,
            diagnostics,
            out modifierErrors
        );

        // TODO
        // ModifierHelpers.ReportDefaultInterfaceImplementationModifiers(accessorsHaveImplementation, mods,
        //                                                             defaultInterfaceImplementationModifiers,
        //                                                             location, diagnostics);

        if (isInterface) {
            mods = ModifierHelpers.AdjustModifiersForAnInterfaceMember(
                mods,
                accessorsHaveImplementation,
                isExplicitInterfaceImplementation,
                forMethod: false
            );
        }

        // TODO Do we care about explicit accessibility?
        return (mods, false);
    }

    private protected override SourcePropertyAccessorSymbol CreateGetAccessorSymbol(
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics) {
        var syntax = (PropertyDeclarationSyntax)belteSyntaxNode;
        var arrowExpression = GetArrowExpression(syntax);

        if (syntax.accessorList is null && arrowExpression is not null) {
            return CreateExpressionBodiedAccessor(
                arrowExpression,
                diagnostics
            );
        } else {
            return CreateAccessorSymbol(GetGetAccessorDeclaration(syntax), isAutoPropertyAccessor, diagnostics);
        }
    }

    private protected override SourcePropertyAccessorSymbol CreateSetAccessorSymbol(
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics) {
        var syntax = (PropertyDeclarationSyntax)belteSyntaxNode;
        Debug.Assert(!(syntax.accessorList is null && GetArrowExpression(syntax) is not null));

        return CreateAccessorSymbol(GetSetAccessorDeclaration(syntax), isAutoPropertyAccessor, diagnostics);
    }

    private SourcePropertyAccessorSymbol CreateAccessorSymbol(
        AccessorDeclarationSyntax syntax,
        bool isAutoPropertyAccessor,
        BelteDiagnosticQueue diagnostics) {
        return SourcePropertyAccessorSymbol.CreateAccessorSymbol(
            containingType,
            this,
            _modifiers,
            syntax,
            isAutoPropertyAccessor,
            diagnostics
        );
    }

    private SourcePropertyAccessorSymbol CreateExpressionBodiedAccessor(
        ArrowExpressionClauseSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        return SourcePropertyAccessorSymbol.CreateAccessorSymbol(
            containingType,
            this,
            _modifiers,
            syntax,
            diagnostics
        );
    }

    private Binder CreateBinderForTypeAndParameters() {
        var compilation = declaringCompilation;
        var syntaxTree = this.syntaxTree;
        var syntax = belteSyntaxNode;
        var binderFactory = compilation.GetBinderFactory(syntaxTree);
        var binder = binderFactory.GetBinder(syntax, syntax, this);
        var modifiers = GetModifierTokensSyntax(syntax);

        var signatureFlags = BinderFlags.SuppressConstraintChecks;

        if (hasLowLevelModifier)
            signatureFlags |= BinderFlags.LowLevelContext;

        return binder.WithAdditionalFlagsAndContainingMember(signatureFlags, this);
    }

    private protected override (TypeWithAnnotations Type, ImmutableArray<ParameterSymbol> Parameters) MakeParametersAndBindType(
        BelteDiagnosticQueue diagnostics) {
        var binder = CreateBinderForTypeAndParameters();
        var syntax = belteSyntaxNode;

        return (ComputeType(binder, syntax, diagnostics), ComputeParameters(binder, syntax, diagnostics));
    }

    private TypeWithAnnotations ComputeType(Binder binder, SyntaxNode syntax, BelteDiagnosticQueue diagnostics) {
        var typeSyntax = GetTypeSyntax(syntax);

        typeSyntax = typeSyntax.SkipRef(out _);
        var type = binder.BindType(typeSyntax, diagnostics);

        if (GetExplicitInterfaceSpecifier() is null && !IsNoMoreVisibleThan(type.type)) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add((this.IsIndexer ? ErrorCode.ERR_BadVisIndexerReturn : ErrorCode.ERR_BadVisPropertyType), Location, this, type.Type);
        }

        if (type.IsVoidType()) {
            // TODO
            throw ExceptionUtilities.Unreachable();
            // diagnostics.Add(ErrorCode.ERR_PropertyCantHaveVoidType, Location, this);
        }

        return type;
    }

    private static ImmutableArray<ParameterSymbol> MakeParameters(
        Binder binder,
        SourcePropertySymbolBase owner,
        ParameterListSyntax parameterListSyntax,
        BelteDiagnosticQueue diagnostics,
        bool addRefConstModifier) {
        if (parameterListSyntax is null)
            return [];

        var parameters = ParameterHelpers.MakeParameters(
            binder,
            owner,
            parameterListSyntax.parameters,
            diagnostics,
            allowRef: false,
            addRefConstModifier: addRefConstModifier,
            allowConst: false
        ).Cast<SourceParameterSymbol, ParameterSymbol>();

        if (parameters.Length == 1 && !owner.isExplicitInterfaceImplementation) {
            var parameterSyntax = parameterListSyntax.parameters[0];

            if (parameterSyntax.defaultValue is not null) {
                // TODO Warning
                throw ExceptionUtilities.Unreachable();
                // SyntaxToken paramNameToken = parameterSyntax.identifier;
                // diagnostics.Add(ErrorCode.WRN_DefaultValueForUnconsumedLocation, paramNameToken.GetLocation(), paramNameToken.ValueText);
            }
        }

        return parameters;
    }

    private ImmutableArray<ParameterSymbol> ComputeParameters(
        Binder binder,
        BelteSyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        var parameterSyntax = GetParameterListSyntax(syntax);

        var parameters = MakeParameters(
            binder,
            this,
            parameterSyntax,
            diagnostics,
            addRefConstModifier: isVirtual || isAbstract
        );

        return parameters;
    }

    internal override void AfterAddingTypeMembersChecks(ConversionsBase conversions, BelteDiagnosticQueue diagnostics) {
        base.AfterAddingTypeMembersChecks(conversions, diagnostics);

        var containingTypeForFileTypeCheck = containingType;

        foreach (var param in parameters) {
            if (!isExplicitInterfaceImplementation && !IsNoMoreVisibleThan(param.type)) {
                // diagnostics.Add(ErrorCode.ERR_BadVisIndexerParam, Location, this, param.Type);
                // TODO
                throw ExceptionUtilities.Unreachable();
            } else if (setMethod is not null && param.name == ParameterSymbol.ValueParameterName) {
                // diagnostics.Add(ErrorCode.ERR_DuplicateGeneratedName, param.TryGetFirstLocation() ?? Location, param.Name);
                // TODO
                throw ExceptionUtilities.Unreachable();
            }
        }
    }

    private static ParameterListSyntax GetParameterListSyntax(BelteSyntaxNode syntax) {
        return null;
    }
}
