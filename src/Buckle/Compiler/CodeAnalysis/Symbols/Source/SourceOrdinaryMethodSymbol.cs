using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceOrdinaryMethodSymbol : SourceOrdinaryMethodSymbolBase {
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
            MakeModifiersAndFlags(syntax, methodKind, diagnostics, out var hasExplicitAccessModifier)) {
        // TODO Eventually will want to have Symbol.CompilationAllowsUnsafe()
        // CheckLowlevelModifier(_modifiers, diagnostics);
        this.hasExplicitAccessModifier = hasExplicitAccessModifier;
        var hasAnyBody = syntax.body is not null;
        location = syntax.identifier.location;

        if (hasAnyBody)
            CheckModifiersForBody(location, diagnostics);

        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, location);

        if (syntax.templateParameterList is null)
            ReportErrorIfHasConstraints(syntax.constraintClauseList, diagnostics);
    }

    internal override TextLocation location { get; }

    internal bool hasExplicitAccessModifier { get; }

    private protected sealed override TextLocation _returnTypeLocation => GetSyntax().returnType.location;

    private bool _hasAnyBody => _flags.hasAnyBody;

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
        MethodDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var name = syntax.identifier.text;

        return syntax.templateParameterList is null
            ? new SourceSimpleOrdinaryMethodSymbol(containingType, name, syntax, MethodKind.Ordinary, diagnostics)
            : new SourceComplexOrdinaryMethodSymbol(containingType, name, syntax, MethodKind.Ordinary, diagnostics);
    }

    internal sealed override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactory, ignoreAccessibility);
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
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
            // TODO
        }

        CheckModifiers(GetSyntax().identifier.location, diagnostics);
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        MethodDeclarationSyntax syntax,
        MethodKind methodKind,
        BelteDiagnosticQueue diagnostics,
        out bool hasExplicitAccessMod) {
        (var declarationModifiers, hasExplicitAccessMod) = MakeModifiers(syntax, diagnostics);

        var flags = new Flags(
            methodKind,
            // TODO See todo in fields, we currently use ref modifier on outer symbol instead of on type
            // syntax.returnType.GetRefKind(),
            ((declarationModifiers & DeclarationModifiers.Ref) != 0) ? RefKind.Ref : RefKind.None,
            declarationModifiers,
            false,
            false,
            syntax.body is not null,
            false
        );

        return (declarationModifiers, flags);
    }

    private static (DeclarationModifiers mods, bool hasExplicitAccessMod) MakeModifiers(
        MethodDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var defaultAccess = DeclarationModifiers.Private;

        var allowedModifiers = DeclarationModifiers.LowLevel
            | DeclarationModifiers.New
            | DeclarationModifiers.Sealed
            | DeclarationModifiers.Abstract
            | DeclarationModifiers.Static
            | DeclarationModifiers.Virtual
            | DeclarationModifiers.Const
            | DeclarationModifiers.AccessibilityMask
            | DeclarationModifiers.Ref
            | DeclarationModifiers.Override;

        bool hasExplicitAccessMod;
        var mods = MakeDeclarationModifiers(syntax, allowedModifiers, diagnostics);

        if ((mods & DeclarationModifiers.AccessibilityMask) == 0) {
            hasExplicitAccessMod = false;
            mods |= defaultAccess;
        } else {
            hasExplicitAccessMod = true;
        }

        return (mods, hasExplicitAccessMod);
    }

    private static DeclarationModifiers MakeDeclarationModifiers(
        MethodDeclarationSyntax syntax,
        DeclarationModifiers allowedModifiers,
        BelteDiagnosticQueue diagnostics) {
        return ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            syntax.modifiers,
            DeclarationModifiers.None,
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
            true,
            isVirtual || isAbstract
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
                    (declaredConstraints[t.ordinal].constraints & (TypeParameterConstraintKinds.Object)) == 0;

                // TODO Add this if Nullable<T> becomes the way to handle nullable types
                // type.TryForceResolve(asPrimitive);
            }
        }
    }

    private void CheckModifiers(TextLocation location, BelteDiagnosticQueue diagnostics) {
        if (declaredAccessibility == Accessibility.Private && (isVirtual || isAbstract || isOverride))
            diagnostics.Push(Error.CannotBePrivateAndVirtualOrAbstract(location, this));
        else if (isOverride && (isNew || isVirtual))
            diagnostics.Push(Error.ConflictingOverrideModifiers(location, this));
        else if (isSealed && !isOverride && !isAbstract)
            diagnostics.Push(Error.SealedNonOverride(location, this));
        else if (returnType.StrippedType().isStatic)
            diagnostics.Push(Error.CannotReturnStatic(location, returnType));
        else if (isAbstract && isSealed)
            diagnostics.Push(Error.AbstractAndSealed(location, this));
        else if (isAbstract && isVirtual)
            diagnostics.Push(Error.AbstractAndVirtual(location, kind.Localize(), this));
        else if (isStatic && isDeclaredConst)
            diagnostics.Push(Error.StaticAndConst(location, this));
        else if (isAbstract && !containingType.isAbstract)
            diagnostics.Push(Error.AbstractInNonAbstractType(location, this, containingType));
        else if (isVirtual && containingType.isSealed)
            diagnostics.Push(Error.VirtualInSealedType(location, this, containingType));
        else if (!_hasAnyBody && !isAbstract)
            diagnostics.Push(Error.NonAbstractMustHaveBody(location, this));
        else if (containingType.isSealed && declaredAccessibility.HasProtected() && !isOverride)
            diagnostics.Push(Error.ProtectedInSealed(location, this));
        else if (containingType.isStatic && !isStatic)
            diagnostics.Push(Error.InstanceMemberInStatic(location, this));
    }
}
