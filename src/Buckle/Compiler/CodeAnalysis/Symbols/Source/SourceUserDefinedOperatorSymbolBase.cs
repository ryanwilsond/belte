using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceUserDefinedOperatorSymbolBase : SourceOrdinaryMethodOrUserDefinedOperatorSymbol {
    private const TypeCompareKind ComparisonForUserDefinedOperators = TypeCompareKind.IgnoreNullability;

    private protected SourceUserDefinedOperatorSymbolBase(
        MethodKind methodKind,
        string name,
        SourceMemberContainerTypeSymbol containingType,
        BelteSyntaxNode syntax,
        DeclarationModifiers modifiers,
        bool hasAnyBody,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            (modifiers, new Flags(methodKind, RefKind.None, modifiers, false, false, hasAnyBody, false))
        ) {
        this.name = name;
        var location = ((OperatorDeclarationSyntax)syntaxReference.node).operatorToken.location;

        if (containingType.isStatic) {
            diagnostics.Push(Error.OperatorInStaticClass(location));
            return;
        }

        if (declaredAccessibility != Accessibility.Public || !isStatic)
            diagnostics.Push(Error.OperatorMustBePublicAndStatic(location));

        if (hasAnyBody && isAbstract)
            diagnostics.Push(Error.AbstractCannotHaveBody(location, this));

        if (!hasAnyBody && !isAbstract)
            diagnostics.Push(Error.NonAbstractMustHaveBody(location, this));

        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, location);
    }

    public sealed override string name { get; }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return [];
    }

    internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return [];
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        var syntax = (OperatorDeclarationSyntax)syntaxReference.node;
        var (returnType, parameters) = MakeParametersAndBindReturnType(syntax, syntax.returnType, diagnostics);

        MethodChecks(returnType, parameters, diagnostics);

        if (containingType.isStatic)
            return;

        CheckValueParameters(diagnostics);
        CheckOperatorSignatures(diagnostics);
    }

    private void CheckOperatorSignatures(BelteDiagnosticQueue diagnostics) {
        if (!DoesOperatorHaveCorrectArity(name, parameterCount))
            return;

        switch (name) {
            case WellKnownMemberNames.UnaryNegationOperatorName:
            case WellKnownMemberNames.UnaryPlusOperatorName:
            case WellKnownMemberNames.LogicalNotOperatorName:
            case WellKnownMemberNames.BitwiseNotOperatorName:
                CheckUnarySignature(diagnostics);
                break;
            case WellKnownMemberNames.IncrementOperatorName:
            case WellKnownMemberNames.DecrementOperatorName:
                CheckIncrementSignature(diagnostics);
                break;

            case WellKnownMemberNames.LeftShiftOperatorName:
            case WellKnownMemberNames.RightShiftOperatorName:
            case WellKnownMemberNames.UnsignedRightShiftOperatorName:
                CheckShiftSignature(diagnostics);
                break;

            case WellKnownMemberNames.EqualityOperatorName:
            case WellKnownMemberNames.InequalityOperatorName:
                if (isAbstract || isVirtual)
                    CheckAbstractEqualitySignature(diagnostics);
                else
                    CheckBinarySignature(diagnostics);

                break;
            default:
                CheckBinarySignature(diagnostics);
                break;
        }
    }

    private void CheckIncrementSignature(BelteDiagnosticQueue diagnostics) {
        // SPEC: A unary ++ or -- operator must take a single parameter of type T or T?
        // SPEC: and it must return that same type or a type derived from it.

        // The native compiler error reporting behavior is not very good in some cases
        // here, both because it reports the wrong errors, and because the wording
        // of the error messages is misleading. The native compiler reports two errors:

        // CS0448: The return type for ++ or -- operator must be the
        //         containing type or derived from the containing type
        //
        // CS0559: The parameter type for ++ or -- operator must be the containing type
        //
        // Neither error message mentions nullable types. But worse, there is a
        // situation in which the native compiler reports a misleading error:
        //
        // struct S { public static S operator ++(S? s) { ... } }
        //
        // This reports CS0559, but that is not the error; the *parameter* is perfectly
        // legal. The error is that the return type does not match the parameter type.
        //
        // I have changed the error message to reflect the true error, and we now
        // report 0448, not 0559, in the given scenario. The error is now:
        //
        // CS0448: The return type for ++ or -- operator must match the parameter type
        //         or be derived from the parameter type
        //
        // However, this now means that we must make *another* change from native compiler
        // behavior. The native compiler would report both 0448 and 0559 when given:
        //
        // struct S { public static int operator ++(int s) { ... } }
        //
        // The previous wording of error 0448 was *correct* in this scenario, but not
        // it is wrong because it *does* match the formal parameter type.
        //
        // The solution is: First see if 0559 must be reported. Only if the formal
        // parameter type is *good* do we then go on to try to report an error against
        // the return type.

        var parameterType = this.GetParameterType(0);
        var useSiteInfo = new CompoundUseSiteInfo<AssemblySymbol>(diagnostics, ContainingAssembly);

        if (!MatchesContainingType(parameterType.StrippedType())) {
            // CS0559: The parameter type for ++ or -- operator must be the containing type
            diagnostics.Add((IsAbstract || IsVirtual) ? ErrorCode.ERR_BadAbstractIncDecSignature : ErrorCode.ERR_BadIncDecSignature, this.GetFirstLocation());
        } else if (!(parameterType.IsTypeParameter() ?
                       this.ReturnType.Equals(parameterType, ComparisonForUserDefinedOperators) :
                       (((IsAbstract || IsVirtual) && IsContainingType(parameterType) && IsSelfConstrainedTypeParameter(this.ReturnType)) ||
                           this.ReturnType.EffectiveTypeNoUseSiteDiagnostics.IsEqualToOrDerivedFrom(parameterType, ComparisonForUserDefinedOperators, useSiteInfo: ref useSiteInfo)))) {
            // CS0448: The return type for ++ or -- operator must match the parameter type
            //         or be derived from the parameter type
            diagnostics.Add((IsAbstract || IsVirtual) ? ErrorCode.ERR_BadAbstractIncDecRetType : ErrorCode.ERR_BadIncDecRetType, this.GetFirstLocation());
        }

        diagnostics.Add(this.GetFirstLocation(), useSiteInfo);
    }

    private void CheckBinarySignature(BelteDiagnosticQueue diagnostics) {
        if (!MatchesContainingType(GetParameterType(0).StrippedType()) &&
            !MatchesContainingType(GetParameterType(1).StrippedType())) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractBinaryOperatorSignature(location));
            else
                diagnostics.Push(Error.BadBinaryOperatorSignature(location));
        }

        CheckReturnIsNotVoid(diagnostics);
    }

    private void CheckAbstractEqualitySignature(BelteDiagnosticQueue diagnostics) {
        if (!IsSelfConstrainedTypeParameter(GetParameterType(0).StrippedType()) &&
            !IsSelfConstrainedTypeParameter(GetParameterType(1).StrippedType())) {
            diagnostics.Push(Error.BadAbstractEqualityOperatorSignature(location, containingType));
        }

        CheckReturnIsNotVoid(diagnostics);
    }

    private void CheckShiftSignature(BelteDiagnosticQueue diagnostics) {
        if (!MatchesContainingType(GetParameterType(0).StrippedType())) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractShiftOperatorSignature(location));
            else
                diagnostics.Push(Error.BadShiftOperatorSignature(location));
        }

        CheckReturnIsNotVoid(diagnostics);
    }

    private void CheckUnarySignature(BelteDiagnosticQueue diagnostics) {
        if (!MatchesContainingType(GetParameterType(0).StrippedType())) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractUnaryOperatorSignature(location));
            else
                diagnostics.Push(Error.BadUnaryOperatorSignature(location));
        }

        CheckReturnIsNotVoid(diagnostics);
    }

    private bool MatchesContainingType(TypeSymbol type) {
        return IsContainingType(type) || ((isAbstract || isVirtual) && IsSelfConstrainedTypeParameter(type));
    }

    private bool IsContainingType(TypeSymbol type) {
        return type.Equals(containingType, ComparisonForUserDefinedOperators);
    }

    internal static bool IsSelfConstrainedTypeParameter(TypeSymbol type, NamedTypeSymbol containingType) {
        return type is TemplateParameterSymbol p &&
            (object)p.containingSymbol == containingType &&
            p.constraintTypes.Any((typeArgument, containingType)
                => typeArgument.type.Equals(containingType, ComparisonForUserDefinedOperators), containingType);
    }

    private bool IsSelfConstrainedTypeParameter(TypeSymbol type) {
        return IsSelfConstrainedTypeParameter(type, containingType);
    }

    private void CheckReturnIsNotVoid(BelteDiagnosticQueue diagnostics) {
        if (returnsVoid)
            diagnostics.Push(Error.OperatorCantReturnVoid(location));
    }

    private static bool DoesOperatorHaveCorrectArity(string name, int parameterCount) {
        switch (name) {
            case WellKnownMemberNames.IncrementOperatorName:
            case WellKnownMemberNames.DecrementOperatorName:
            case WellKnownMemberNames.UnaryNegationOperatorName:
            case WellKnownMemberNames.UnaryPlusOperatorName:
            case WellKnownMemberNames.LogicalNotOperatorName:
            case WellKnownMemberNames.BitwiseNotOperatorName:
                return parameterCount == 1;
            default:
                return parameterCount == 2;
        }
    }

    private void CheckValueParameters(BelteDiagnosticQueue diagnostics) {
        foreach (var parameter in parameters) {
            if (parameter.refKind != RefKind.None) {
                // TODO
                // diagnostics.Push(Error.OperatorCannotHaveRefParameters(parameter.syntaxReference.location));
                break;
            }
        }
    }

    private protected (TypeWithAnnotations ReturnType, ImmutableArray<ParameterSymbol> Parameters)
        MakeParametersAndBindReturnType(
            BaseMethodDeclarationSyntax declarationSyntax,
            TypeSyntax returnTypeSyntax,
            BelteDiagnosticQueue diagnostics) {
        TypeWithAnnotations returnType;
        ImmutableArray<ParameterSymbol> parameters;

        var binder = declaringCompilation.GetBinderFactory(declarationSyntax.syntaxTree)
            .GetBinder(returnTypeSyntax, declarationSyntax, this);

        var signatureBinder = binder.WithAdditionalFlags(BinderFlags.SuppressConstraintChecks);

        parameters = ParameterHelpers.MakeParameters(
            signatureBinder,
            this,
            declarationSyntax.parameterList.parameters,
            diagnostics,
            true,
            isVirtual || isAbstract
        ).Cast<SourceParameterSymbol, ParameterSymbol>();

        returnType = signatureBinder.BindType(returnTypeSyntax, diagnostics);

        return (returnType, parameters);
    }

    private protected static DeclarationModifiers MakeDeclarationModifiers(
        BaseMethodDeclarationSyntax syntax,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        var defaultAccess = DeclarationModifiers.Private;
        var allowedModifiers = DeclarationModifiers.Static
            | DeclarationModifiers.LowLevel
            | DeclarationModifiers.AccessibilityMask;

        var result = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            syntax.modifiers,
            defaultAccess,
            allowedModifiers,
            location,
            diagnostics,
            out _
        );

        return result;
    }
}
