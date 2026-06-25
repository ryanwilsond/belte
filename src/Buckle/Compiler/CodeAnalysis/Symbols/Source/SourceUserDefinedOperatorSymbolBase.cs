using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceUserDefinedOperatorSymbolBase : SourceOrdinaryMethodOrUserDefinedOperatorSymbol {
    private const TypeCompareKind ComparisonForUserDefinedOperators = TypeCompareKind.IgnoreTupleNames;
    private readonly TypeSymbol _fieldExplicitInterfaceType;

    private protected SourceUserDefinedOperatorSymbolBase(
        MethodKind methodKind,
        TypeSymbol explicitInterfaceType,
        string name,
        SourceMemberContainerTypeSymbol containingType,
        TextLocation location,
        BelteSyntaxNode syntax,
        RefKind refKind,
        DeclarationModifiers modifiers,
        bool hasAnyBody,
        BelteDiagnosticQueue diagnostics)
        : base(
            containingType,
            new SyntaxReference(syntax),
            location,
            (modifiers, new Flags(methodKind, refKind, modifiers, false, false, hasAnyBody, false))
        ) {
        _fieldExplicitInterfaceType = explicitInterfaceType;
        this.name = name;

        if (this.containingType.isInterface &&
            !(isAbstract || isVirtual) && !isExplicitInterfaceImplementation &&
            !(syntax is OperatorDeclarationSyntax { operatorToken: var opToken } &&
                opToken.kind is not (SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken))) {
            diagnostics.Push(Error.InterfacesCantContainConversionOrEqualityOperators(location));
            return;
        }

        if (containingType.isStatic) {
            diagnostics.Push(Error.OperatorInStaticClass(location));
            return;
        }

        if (isExplicitInterfaceImplementation) {
            if (!isStatic)
                diagnostics.Push(Error.ExplicitImplementationOfOperatorsMustBeStatic(location, this));
        } else if (declaredAccessibility != Accessibility.Public || !isStatic) {
            diagnostics.Push(Error.OperatorMustBePublicAndStatic(location));
        }

        if (isAbstract && isExtern) {
            diagnostics.Push(Error.AbstractAndExtern(location, this));
        } else if (isAbstract && isVirtual) {
            diagnostics.Push(Error.AbstractAndVirtual(location, kind.Localize(), this));
        } else if (hasAnyBody && (isExtern || isAbstract)) {
            if (isExtern)
                diagnostics.Push(Error.ExternCannotHaveBody(location, this));
            else
                diagnostics.Push(Error.AbstractCannotHaveBody(location, this));
        } else if (!hasAnyBody && !isAbstract && !isExtern) {
            diagnostics.Push(Error.NonAbstractMustHaveBody(location, this));
        } else if (isOverride && (isNew || isVirtual)) {
            diagnostics.Push(Error.ConflictingOverrideModifiers(location, this));
        } else if (isSealed && !isOverride &&
            !(isExplicitInterfaceImplementation && containingType.isInterface && isAbstract)) {
            diagnostics.Push(Error.SealedNonOverride(location, this));
        } else if (isAbstract && isSealed && !isExplicitInterfaceImplementation) {
            diagnostics.Push(Error.AbstractAndSealed(location, this));
        } else if (isAbstract && !containingType.isAbstract && !containingType.isInterface) {
            diagnostics.Push(Error.AbstractInNonAbstractType(location, this, containingType));
        } else if (isVirtual && containingType.isSealed) {
            diagnostics.Push(Error.VirtualInSealedType(location, this, containingType));
        }

        ModifierHelpers.CheckAccessibility(_modifiers, diagnostics, location);
    }

    public sealed override string name { get; }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    private protected sealed override TypeSymbol _explicitInterfaceType => _fieldExplicitInterfaceType;

    internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return [];
    }

    internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return [];
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        var (returnType, parameters) = MakeParametersAndBindReturnType(diagnostics);

        MethodChecks(returnType, parameters, diagnostics);

        if (containingType.isStatic)
            return;

        CheckReturnRefKind(diagnostics);
        CheckValueParameters(diagnostics);
        CheckOperatorSignatures(diagnostics);
    }

    private protected abstract (TypeWithAnnotations ReturnType, ImmutableArray<ParameterSymbol> Parameters)
        MakeParametersAndBindReturnType(BelteDiagnosticQueue diagnostics);

    private void CheckOperatorSignatures(BelteDiagnosticQueue diagnostics) {
        if (methodKind == MethodKind.Literal) {
            CheckLiteralOperatorSignature(diagnostics);
            return;
        }

        if (!DoesOperatorHaveCorrectArity(name, parameterCount))
            return;

        switch (name) {
            case WellKnownMemberNames.ImplicitConversionName:
            case WellKnownMemberNames.ExplicitConversionName:
                CheckUserDefinedConversionSignature(diagnostics);
                break;
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
            case WellKnownMemberNames.LengthOperatorName:
                CheckLengthSignature(diagnostics);
                break;
            case WellKnownMemberNames.IterOperatorName:
                CheckIterSignature(diagnostics);
                break;
            case WellKnownMemberNames.EqualityOperatorName:
            case WellKnownMemberNames.InequalityOperatorName:
                if (IsInInterfaceAndAbstractOrVirtual())
                    CheckAbstractEqualitySignature(diagnostics);
                else
                    CheckBinarySignature(diagnostics);

                break;
            default:
                CheckBinarySignature(diagnostics);
                break;
        }
    }

    private protected sealed override MethodSymbol FindExplicitlyImplementedMethod(BelteDiagnosticQueue diagnostics) {
        if (_explicitInterfaceType is object) {
            string interfaceMethodName;
            ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifier;

            switch (syntaxReference.node) {
                case OperatorDeclarationSyntax operatorDeclaration:
                    interfaceMethodName = SyntaxFacts.GetOperatorMemberName(operatorDeclaration);
                    explicitInterfaceSpecifier = operatorDeclaration.explicitInterfaceSpecifier;
                    break;
                case ConversionDeclarationSyntax conversionDeclaration:
                    interfaceMethodName = SyntaxFacts.GetOperatorMemberName(conversionDeclaration);
                    explicitInterfaceSpecifier = conversionDeclaration.explicitInterfaceSpecifier;
                    break;
                default:
                    throw ExceptionUtilities.Unreachable();
            }

            return this.FindExplicitlyImplementedMethod(
                isOperator: true,
                _explicitInterfaceType,
                interfaceMethodName,
                explicitInterfaceSpecifier,
                diagnostics
            );
        }

        return null;
    }

    private void CheckUserDefinedConversionSignature(BelteDiagnosticQueue diagnostics) {
        CheckReturnIsNotVoid(diagnostics);

        var source = GetParameterType(0);
        var target = returnType;
        var source0 = source.StrippedType();
        var target0 = target.StrippedType();

        if (source0.IsInterfaceType() || target0.IsInterfaceType()) {
            diagnostics.Push(Error.ConversionWithInterface(location, this));
            return;
        }

        if (!MatchesContainingType(source0) &&
            !MatchesContainingType(target0) &&
            !MatchesContainingType(source) &&
            !MatchesContainingType(target)) {
            if (IsInInterfaceAndAbstractOrVirtual())
                diagnostics.Push(Error.AbstractConversionNotInvolvingContainedType(location));
            else
                diagnostics.Push(Error.ConversionNotInvolvingContainedType(location));

            return;
        }

        if ((containingType.specialType == SpecialType.Nullable)
                ? source.Equals(target, ComparisonForUserDefinedOperators)
                : source0.Equals(target0, ComparisonForUserDefinedOperators)) {
            diagnostics.Push(Error.IdentityConversion(location));
            return;
        }

        TypeSymbol same;
        TypeSymbol different;

        if (MatchesContainingType(source0)) {
            same = source;
            different = target;
        } else {
            same = target;
            different = source;
        }

        if (different.IsClassType() && !same.IsTemplateParameter()) {
            if (same.IsDerivedFrom(different, ComparisonForUserDefinedOperators))
                diagnostics.Push(Error.ConversionWithBase(location, this));
            else if (different.IsDerivedFrom(same, ComparisonForUserDefinedOperators))
                diagnostics.Push(Error.ConversionWithDerived(location, this));
        }
    }

    private bool IsInInterfaceAndAbstractOrVirtual() {
        return containingType.isInterface && (isAbstract || isVirtual);
    }

    private void CheckLiteralOperatorSignature(BelteDiagnosticQueue diagnostics) {
        if (!(((isAbstract || isVirtual) && IsSelfConstrainedTypeParameter(returnType)) ||
            returnType.EffectiveType().IsEqualToOrDerivedFrom(containingType, ComparisonForUserDefinedOperators))) {
            diagnostics.Push(Error.BadLiteralOperatorReturnType(location));
        }

        if (parameterCount != 1) {
            diagnostics.Push(Error.LiteralOperatorMustHaveSingleParameter(location));
            return;
        }

        // TODO This disallows user-defined implicit conversions to literal operators
        // TODO This is probably what we want, but is technically more restrictive than we have to be
        if (!IsValidExtendedLiteralType(GetParameterType(0).StrippedType()))
            diagnostics.Push(Error.BadLiteralOperatorParameterType(location));

        static bool IsValidExtendedLiteralType(TypeSymbol type) {
            if (type.specialType.IsValidExtendedLiteral())
                return true;

            if (type is PointerTypeSymbol pointer && pointer.pointedAtType.specialType.IsValidPointerExtendedLiteral())
                return true;

            return false;
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

    private void CheckLengthSignature(BelteDiagnosticQueue diagnostics) {
        if (!MatchesContainingType(GetParameterType(0).StrippedType())) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractUnaryOperatorSignature(location));
            else
                diagnostics.Push(Error.BadUnaryOperatorSignature(location));
        }

        if (returnType.specialType != SpecialType.Int)
            diagnostics.Push(Error.LengthMustReturnInt(location));
    }

    private void CheckIterSignature(BelteDiagnosticQueue diagnostics) {
        if (!MatchesContainingType(GetParameterType(0).StrippedType())) {
            if (isAbstract || isVirtual)
                diagnostics.Push(Error.BadAbstractUnaryOperatorSignature(location));
            else
                diagnostics.Push(Error.BadUnaryOperatorSignature(location));
        }

        if (!returnType.originalDefinition.Equals(CorLibrary.GetWellKnownType(WellKnownType.Enumerator)))
            diagnostics.Push(Error.IterMustReturnEnumerator(location));
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
            case WellKnownMemberNames.LengthOperatorName:
            case WellKnownMemberNames.IterOperatorName:
            case WellKnownMemberNames.ImplicitConversionName:
            case WellKnownMemberNames.ExplicitConversionName:
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
            allowRef: true,
            isVirtual || isAbstract,
            allowConst: true
        ).Cast<SourceParameterSymbol, ParameterSymbol>();

        var syntax = returnTypeSyntax.SkipRef(out _);
        returnType = signatureBinder.BindType(syntax, diagnostics);

        return (returnType, parameters);
    }

    private protected static DeclarationModifiers MakeDeclarationModifiers(
        NamedTypeSymbol containingType,
        MethodKind methodKind,
        BaseMethodDeclarationSyntax syntax,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        var inInterface = containingType.isInterface;
        var isExplicitInterfaceImplementation = methodKind == MethodKind.ExplicitInterfaceImplementation;

        var defaultAccess = inInterface && !isExplicitInterfaceImplementation
            ? DeclarationModifiers.Public
            : (containingType.IsStructType() || containingType.IsFileScoped())
                ? DeclarationModifiers.Public
                : DeclarationModifiers.Private;

        var allowedModifiers = DeclarationModifiers.Extern
                             | DeclarationModifiers.LowLevel
                             | DeclarationModifiers.Static;

        if (!isExplicitInterfaceImplementation) {
            allowedModifiers |= DeclarationModifiers.AccessibilityMask;

            if (inInterface) {
                allowedModifiers |= DeclarationModifiers.Abstract | DeclarationModifiers.Virtual;

                if (syntax is OperatorDeclarationSyntax { operatorToken: var opToken } &&
                    opToken.kind is not (SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken)) {
                    allowedModifiers |= DeclarationModifiers.Sealed;
                }
            }
        } else if (inInterface) {
            allowedModifiers |= DeclarationModifiers.Abstract;
        }

        var result = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            syntax.modifiers,
            inInterface,
            defaultAccess,
            allowedModifiers,
            location,
            diagnostics,
            out _
        );

        if (inInterface) {
            if ((result & (DeclarationModifiers.Abstract | DeclarationModifiers.Virtual | DeclarationModifiers.Sealed)) != 0) {
                if ((result & DeclarationModifiers.Sealed) != 0 &&
                    (result & (DeclarationModifiers.Abstract | DeclarationModifiers.Virtual)) != 0) {
                    diagnostics.Push(Error.InvalidModifier(
                        location,
                        ModifierHelpers.ConvertSingleModifierToSyntaxText(DeclarationModifiers.Sealed)
                    ));
                }

                result &= ~DeclarationModifiers.Sealed;
            }
        }

        if (isExplicitInterfaceImplementation) {
            if ((result & DeclarationModifiers.Abstract) != 0)
                result |= DeclarationModifiers.Sealed;
        }

        return result;
    }
}
