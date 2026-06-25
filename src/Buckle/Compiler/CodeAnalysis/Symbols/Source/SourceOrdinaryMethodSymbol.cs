using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceOrdinaryMethodSymbol : SourceOrdinaryMethodSymbolBase {
    private readonly MethodDeclarationSyntax _syntax;

    private MethodSymbol _lazyReverseMethod;
    private MethodSymbol _lazyStateMethod;
    private ImmutableArray<FieldSymbol> _lazyInitFields;

    private SourceOrdinaryMethodSymbol(
        NamedTypeSymbol containingType,
        string name,
        MethodDeclarationSyntax syntax,
        MethodKind methodKind,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            name,
            syntax,
            syntax.identifier.location,
            MakeModifiersAndFlags(containingType, syntax, methodKind, diagnostics, out var hasExplicitAccessModifier)) {
        // TODO Eventually will want to have Symbol.CompilationAllowsUnsafe()
        // CheckLowlevelModifier(_modifiers, diagnostics);
        this.hasExplicitAccessModifier = hasExplicitAccessModifier;
        var hasAnyBody = syntax.body is not null;
        _syntax = syntax;

        ReportDefaultInterfaceImplementation(location, hasAnyBody, diagnostics);

        if (hasAnyBody)
            CheckModifiersForBody(location, diagnostics);

        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, location);

        if (syntax.templateParameterList is null)
            ReportErrorIfHasConstraints(syntax.constraintClauseList, diagnostics);
    }

    internal bool hasExplicitAccessModifier { get; }

    private protected sealed override TextLocation _returnTypeLocation => GetSyntax().returnType.location;

    private bool _hasAnyBody => _flags.hasAnyBody;

    internal override bool isReversible => _syntax.reverseClause is not null;

    internal override bool hasReversalState => _syntax.stateClause is not null;

    internal override MethodSymbol reverseMethod {
        get {
            if (isReversible && _lazyReverseMethod is null)
                Interlocked.CompareExchange(ref _lazyReverseMethod, MakeReverseMethod(_syntax.reverseClause), null);

            return _lazyReverseMethod;
        }
    }

    internal override MethodSymbol stateMethod {
        get {
            if (hasReversalState && _lazyStateMethod is null)
                Interlocked.CompareExchange(ref _lazyStateMethod, MakeStateMethod(_syntax.stateClause), null);

            return _lazyStateMethod;
        }
    }

    internal override ImmutableArray<FieldSymbol> initFields {
        get {
            if (_lazyInitFields == default) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var initFields = MakeInitFields(_syntax.initConstraintClauseSyntax, diagnostics);

                if (ImmutableInterlocked.InterlockedInitialize(ref _lazyInitFields, initFields)) {
                    AddDeclarationDiagnostics(diagnostics);
                    diagnostics.Free();
                }
            }

            return _lazyInitFields;
        }
    }

    private SyntaxList<AttributeListSyntax> _attributeDeclarationSyntaxList {
        get {
            if (containingType is SourceMemberContainerTypeSymbol sourceContainer &&
                sourceContainer.anyMemberHasAttributes) {
                return GetSyntax().attributeLists;
            }

            return default;
        }
    }

    internal static SourceOrdinaryMethodSymbol CreateMethodSymbol(
        NamedTypeSymbol containingType,
        Binder bodyBinder,
        MethodDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var interfaceSpecifier = syntax.explicitInterfaceSpecifier;
        var nameToken = syntax.identifier;

        var name = ExplicitInterfaceHelpers.GetMemberNameAndInterfaceSymbol(
            bodyBinder,
            syntax.modifiers,
            interfaceSpecifier,
            nameToken.valueText,
            diagnostics,
            out var explicitInterfaceType,
            aliasQualifier: out _
        );

        var methodKind = interfaceSpecifier is null
            ? MethodKind.Ordinary
            : MethodKind.ExplicitInterfaceImplementation;

        return syntax.templateParameterList is null && explicitInterfaceType is null
            ? new SourceSimpleOrdinaryMethodSymbol(containingType, name, syntax, methodKind, diagnostics)
            : new SourceComplexOrdinaryMethodSymbol(
                containingType,
                explicitInterfaceType,
                name,
                syntax,
                methodKind,
                diagnostics
            );
    }

    internal sealed override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactory, ignoreAccessibility);
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        if (containingType is SourceMemberContainerTypeSymbol sourceType) {
            var inheritedAttributes = sourceType.GetInheritedAttributeListsForMember(_syntax);

            if (inheritedAttributes is not null)
                return OneOrMany.Create(_attributeDeclarationSyntaxList, inheritedAttributes);
        }

        return OneOrMany.Create(_attributeDeclarationSyntaxList);
    }

    internal MethodDeclarationSyntax GetSyntax() {
        return (MethodDeclarationSyntax)syntaxNode;
    }

    private protected sealed override int GetParameterCountFromSyntax() {
        return GetSyntax().parameterList.parameters.Count;
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        var (returnType, parameters, declaredConstraints) = MakeParametersAndBindReturnType(diagnostics);
        var overriddenMethod = MethodChecks(returnType, parameters, diagnostics);

        if (!declaredConstraints.IsDefault && overriddenMethod is not null) {
            // TODO constraints
        }

        CheckModifiers(
            methodKind == MethodKind.ExplicitInterfaceImplementation,
            GetSyntax().identifier.location,
            diagnostics
        );

        _ = initFields;
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        NamedTypeSymbol containingType,
        MethodDeclarationSyntax syntax,
        MethodKind methodKind,
        BelteDiagnosticQueue diagnostics,
        out bool hasExplicitAccessMod) {
        (var declarationModifiers, hasExplicitAccessMod) = MakeModifiers(
            containingType,
            methodKind,
            syntax.HasAnyBody(),
            syntax,
            diagnostics
        );

        var flags = new Flags(
            methodKind,
            syntax.returnType.GetRefKind(),
            declarationModifiers,
            false,
            false,
            syntax.body is not null,
            false
        );

        return (declarationModifiers, flags);
    }

    private static (DeclarationModifiers mods, bool hasExplicitAccessMod) MakeModifiers(
        NamedTypeSymbol containingType,
        MethodKind methodKind,
        bool hasBody,
        MethodDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var isInterface = containingType.isInterface;
        var isExplicitInterfaceImplementation = methodKind == MethodKind.ExplicitInterfaceImplementation;

        var inheritedModifiers = containingType is SourceMemberContainerTypeSymbol sourceType
            ? sourceType.GetInheritedModifiersForMember(syntax)
            : DeclarationModifiers.None;

        var inheritedAccess = inheritedModifiers & DeclarationModifiers.AccessibilityMask;

        var defaultAccess = isInterface && !isExplicitInterfaceImplementation
            ? DeclarationModifiers.None
            : inheritedAccess != DeclarationModifiers.None
                ? inheritedAccess
                : (containingType.IsStructType() || containingType.IsFileScoped())
                    ? DeclarationModifiers.Public
                    : DeclarationModifiers.Private;

        var allowedModifiers =
              DeclarationModifiers.LowLevel
            | DeclarationModifiers.Extern
            | DeclarationModifiers.Ref
            | DeclarationModifiers.Const;

        var defaultInterfaceImplementationModifiers = DeclarationModifiers.None;

        if (!isExplicitInterfaceImplementation) {
            allowedModifiers |= DeclarationModifiers.Static |
                                DeclarationModifiers.AccessibilityMask |
                                DeclarationModifiers.New |
                                DeclarationModifiers.Sealed |
                                DeclarationModifiers.Abstract |
                                DeclarationModifiers.Virtual;

            if (!isInterface) {
                allowedModifiers |= DeclarationModifiers.Override;
            } else {
                defaultInterfaceImplementationModifiers |= DeclarationModifiers.Sealed |
                                                           DeclarationModifiers.Abstract |
                                                           DeclarationModifiers.Static |
                                                           DeclarationModifiers.Virtual |
                                                           DeclarationModifiers.Extern |
                                                           DeclarationModifiers.AccessibilityMask;
            }
        } else {
            if (isInterface)
                allowedModifiers |= DeclarationModifiers.Abstract;

            allowedModifiers |= DeclarationModifiers.Static;
        }

        // TODO Use defaultInterfaceImplementationModifiers

        bool hasExplicitAccessMod;
        var mods = MakeDeclarationModifiers(containingType, syntax, allowedModifiers, diagnostics);

        if ((mods & DeclarationModifiers.AccessibilityMask) == 0) {
            hasExplicitAccessMod = false;
            mods |= defaultAccess;
        } else {
            hasExplicitAccessMod = true;
        }

        mods = AddImpliedModifiers(mods, isInterface, methodKind, hasBody);
        mods |= inheritedModifiers;
        return (mods, hasExplicitAccessMod);
    }

    private static DeclarationModifiers AddImpliedModifiers(
        DeclarationModifiers mods,
        bool containingTypeIsInterface,
        MethodKind methodKind,
        bool hasBody) {
        if (containingTypeIsInterface) {
            mods = ModifierHelpers.AdjustModifiersForAnInterfaceMember(
                mods,
                hasBody,
                methodKind == MethodKind.ExplicitInterfaceImplementation,
                forMethod: true
            );
        } else if (methodKind == MethodKind.ExplicitInterfaceImplementation) {
            mods = (mods & ~DeclarationModifiers.AccessibilityMask) | DeclarationModifiers.Private;
        }

        return mods;
    }

    private static DeclarationModifiers MakeDeclarationModifiers(
        NamedTypeSymbol containingType,
        MethodDeclarationSyntax syntax,
        DeclarationModifiers allowedModifiers,
        BelteDiagnosticQueue diagnostics) {
        return ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            syntax.modifiers,
            containingType.isInterface,
            (containingType.IsStructType() || containingType.IsFileScoped())
                ? DeclarationModifiers.Public
                : DeclarationModifiers.None,
            allowedModifiers,
            syntax.identifier.location,
            diagnostics,
            out _
        );
    }

    private (TypeWithAnnotations returnType,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<TypeParameterConstraintClause> declaredConstraints)
        MakeParametersAndBindReturnType(BelteDiagnosticQueue diagnostics) {
        var syntax = (MethodDeclarationSyntax)syntaxNode;
        var returnTypeSyntax = syntax.returnType;
        var withTemplateParametersBinder = declaringCompilation
            .GetBinderFactory(syntax.syntaxTree)
            .GetBinder(returnTypeSyntax, syntax, this);

        var signatureBinder = withTemplateParametersBinder.WithAdditionalFlagsAndContainingMember(
            BinderFlags.SuppressConstraintChecks,
            this
        );

        var parameters = ParameterHelpers.MakeParameters(
            signatureBinder,
            this,
            syntax.parameterList.parameters,
            diagnostics,
            allowRef: true,
            isVirtual || isAbstract,
            allowConst: true
        ).Cast<SourceParameterSymbol, ParameterSymbol>();

        returnTypeSyntax = returnTypeSyntax.SkipRef(out _);
        var returnType = signatureBinder.BindType(returnTypeSyntax, diagnostics);

        ImmutableArray<TypeParameterConstraintClause> declaredConstraints = default;

        if (arity != 0 && isOverride) {
            if (syntax.constraintClauseList.constraintClauses.Count > 0) {
                declaredConstraints = signatureBinder
                    .WithAdditionalFlags(BinderFlags.TemplateConstraintsClause | BinderFlags.SuppressConstraintChecks)
                    .BindTypeParameterConstraintClauses(
                        this,
                        templateParameters,
                        syntax.templateParameterList,
                        syntax.constraintClauseList.constraintClauses,
                        diagnostics
                    );
            }

            foreach (var parameter in parameters)
                ForceMethodTemplateParameters(parameter.typeWithAnnotations, this, declaredConstraints);

            ForceMethodTemplateParameters(returnType, this, declaredConstraints);
        }

        return (returnType, parameters, declaredConstraints);

        static void ForceMethodTemplateParameters(
            TypeWithAnnotations type,
            SourceOrdinaryMethodSymbol method,
            ImmutableArray<TypeParameterConstraintClause> declaredConstraints) {
            if (type.type is TemplateParameterSymbol t && (object)t.declaringMethod == method) {
                var asPrimitive = declaredConstraints.IsDefault ||
                    (declaredConstraints[t.ordinal].constraints & (TypeParameterConstraintKinds.ReferenceType)) == 0;

                // TODO Add this if Nullable<T> becomes the way to handle nullable types
                // type.TryForceResolve(asPrimitive);
            }
        }
    }

    private void CheckModifiers(
        bool isExplicitInterfaceImplementation,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        var isExplicitInterfaceImplementationInInterface = isExplicitInterfaceImplementation &&
            containingType.isInterface;

        if (declaredAccessibility == Accessibility.Private &&
            (isVirtual || (isAbstract && !isExplicitInterfaceImplementationInInterface) || isOverride))
            diagnostics.Push(Error.CannotBePrivateAndVirtualOrAbstract(location, this));
        else if (isOverride && (isNew || isVirtual))
            diagnostics.Push(Error.ConflictingOverrideModifiers(location, this));
        else if (isSealed && !isOverride && !(isExplicitInterfaceImplementationInInterface && isAbstract))
            diagnostics.Push(Error.SealedNonOverride(location, this));
        else if (returnType.StrippedType().isStatic)
            diagnostics.Push(Error.CannotReturnStatic(location, returnType));
        else if (isAbstract && isSealed && !isExplicitInterfaceImplementationInInterface)
            diagnostics.Push(Error.AbstractAndSealed(location, this));
        else if (isAbstract && isExtern)
            diagnostics.Push(Error.AbstractAndExtern(location, this));
        else if (isAbstract && isVirtual)
            diagnostics.Push(Error.AbstractAndVirtual(location, kind.Localize(), this));
        else if (isStatic && isDeclaredConst)
            diagnostics.Push(Error.StaticAndConst(location, this));
        else if (isAbstract && !containingType.isAbstract)
            diagnostics.Push(Error.AbstractInNonAbstractType(location, this, containingType));
        else if (isVirtual && containingType.isSealed)
            diagnostics.Push(Error.VirtualInSealedType(location, this, containingType));
        else if (!_hasAnyBody && !isAbstract && !isExtern)
            diagnostics.Push(Error.NonAbstractMustHaveBody(location, this));
        else if (containingType.isSealed && declaredAccessibility.HasProtected() && !isOverride)
            diagnostics.Push(Warning.ProtectedInSealed(location, this));
        else if (containingType.isStatic && !isStatic)
            diagnostics.Push(Error.InstanceMemberInStatic(location, this));
    }

    private SourceReverseMethodSymbol MakeReverseMethod(ReverseClauseSyntax syntax) {
        return new SourceReverseMethodSymbol(syntax, containingType, this, stateMethod);
    }

    private SourceStateMethodSymbol MakeStateMethod(StateClauseSyntax syntax) {
        return new SourceStateMethodSymbol(syntax, containingType, this);
    }

    private ImmutableArray<FieldSymbol> MakeInitFields(
        InitConstraintClauseSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        if (syntax is null)
            return [];

        var members = containingType.GetMembers();
        var fields = members.Where(m => m is FieldSymbol).Select(m => m as FieldSymbol).ToImmutableArray();

        var builder = ArrayBuilder<FieldSymbol>.GetInstance();

        foreach (var identifier in syntax.names) {
            var name = identifier.identifier.valueText;

            if (!members.Any(m => m.name == name)) {
                diagnostics.Push(Error.NoSuchMember(identifier.location, containingType, name));
                continue;
            }

            var candidates = fields.WhereAsArray(f => f.name == name);

            if (candidates.Length == 0)
                diagnostics.Push(Error.NoSuchField(identifier.location, containingType, name));
            else if (candidates.Length == 1)
                builder.Add(candidates[0]);
            else
                // Pretty sure this is unreachable, but if not we can make use of some of the unused errors around ambiguous members
                throw ExceptionUtilities.Unreachable();
        }

        return builder.ToImmutableAndFree();
    }
}
