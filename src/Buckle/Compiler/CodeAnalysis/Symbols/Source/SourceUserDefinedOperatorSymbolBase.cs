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
        RefKind refKind,
        DeclarationModifiers modifiers,
        bool hasAnyBody,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            (modifiers, new Flags(methodKind, refKind, modifiers, false, false, hasAnyBody, false))
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

        CheckReturnRefKind(diagnostics);
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
        var parameterType = GetParameterType(0);

        if (!MatchesContainingType(parameterType.StrippedType())) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractIncrementOperatorSignature(location));
            else
                diagnostics.Push(Error.BadIncrementOperatorSignature(location));
        } else if (!(parameterType.IsTemplateParameter()
                ? returnType.Equals(parameterType, ComparisonForUserDefinedOperators)
                : (((isAbstract || isVirtual) &&
                        IsContainingType(parameterType) &&
                        IsSelfConstrainedTypeParameter(returnType)) ||
                    returnType.EffectiveType()
                        .IsEqualToOrDerivedFrom(parameterType, ComparisonForUserDefinedOperators)))) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractIncrementReturnType(location));
            else
                diagnostics.Push(Error.BadIncrementReturnType(location));
        }
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
                diagnostics.Push(Error.OperatorRefParameter(location));
                break;
            }
        }
    }

    private void CheckReturnRefKind(BelteDiagnosticQueue diagnostics) {
        if (refKind != RefKind.None && name != WellKnownMemberNames.IndexOperatorName)
            diagnostics.Push(Error.OperatorRefReturn(location));
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

        var syntax = returnTypeSyntax.SkipRef(out _);
        returnType = signatureBinder.BindType(syntax, diagnostics);

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
