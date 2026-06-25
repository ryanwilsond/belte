using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceReverseMethodSymbol : SourceMemberMethodSymbol {
    private readonly ReverseClauseSyntax _syntax;
    private readonly SourceMemberMethodSymbol _containingMethod;
    private readonly MethodSymbol _stateMethod;

    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TypeWithAnnotations _lazyReturnType;

    internal SourceReverseMethodSymbol(
        ReverseClauseSyntax syntax,
        NamedTypeSymbol containingType,
        SourceMemberMethodSymbol containingMethod,
        MethodSymbol stateMethod)
        : base(
            containingType,
            new SyntaxReference(syntax),
            syntax.keyword.location,
            MakeModifiersAndFlags(syntax, containingMethod)
        ) {
        _syntax = syntax;
        _containingMethod = containingMethod;
        _stateMethod = stateMethod;
        name = GeneratedNames.MakeReverseMethodName(containingMethod.name);
    }

    public override string name { get; }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    internal sealed override int parameterCount {
        get {
            if (_lazyParameters.IsDefault)
                return _syntax.identifier is null ? 0 : 1;

            return _lazyParameters.Length;
        }
    }

    internal sealed override ImmutableArray<ParameterSymbol> parameters {
        get {
            LazyMethodChecks();
            return _lazyParameters;
        }
    }

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            LazyMethodChecks();
            return _lazyReturnType;
        }
    }

    internal sealed override void AfterAddingTypeMembersChecks(BelteDiagnosticQueue diagnostics) {
        base.AfterAddingTypeMembersChecks(diagnostics);

        foreach (var parameter in parameters)
            parameter.type.CheckAllConstraints(parameter.syntaxReference.location, diagnostics);
    }

    internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return [];
    }

    internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return [];
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetReturnTypeAttributeDeclarations() {
        return OneOrMany.Create(default(SyntaxList<AttributeListSyntax>));
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactory, ignoreAccessibility);
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) {
        var targetMethod = _stateMethod ?? _containingMethod;
        var targetRefKind = _stateMethod is null ? _containingMethod.refKind : RefKind.None;
        var targetTypeWithAnnotations = _stateMethod is null
            ? _containingMethod.returnTypeWithAnnotations
            : _stateMethod.returnType.tupleElementTypes[1].type;

        if (_syntax.identifier is not null) {
            var hasError = false;

            if (targetTypeWithAnnotations.IsVoidType()) {
                diagnostics.Push(Error.InvalidReverseParameter(location));
                hasError = true;
            }

            if (_syntax.type is null) {
                var parameter = SourceParameterSymbol.CreateReverseParameter(
                    this,
                    targetTypeWithAnnotations,
                    _syntax,
                    _syntax.identifier.location,
                    targetRefKind,
                    _syntax.identifier.valueText
                );

                _lazyParameters = [parameter];
            } else {
                var binderFactory = declaringCompilation.GetBinderFactory(_syntax.syntaxTree);

                var bodyBinder = binderFactory.GetBinder(_syntax, _syntax, this).WithContainingMember(this);
                var signatureBinder = bodyBinder
                    .WithAdditionalFlagsAndContainingMember(BinderFlags.SuppressConstraintChecks, this);

                var typeWithAnnotations = signatureBinder.BindType(
                    _syntax.type.SkipRef(out var refKind),
                    hasError ? BelteDiagnosticQueue.Discarded : diagnostics
                );

                var type = typeWithAnnotations.type;

                var parameter = SourceParameterSymbol.CreateReverseParameter(
                    this,
                    typeWithAnnotations,
                    _syntax,
                    _syntax.identifier.location,
                    refKind,
                    _syntax.identifier.valueText
                );

                _lazyParameters = [parameter];

                if (!hasError) {
                    var returnType = targetTypeWithAnnotations.type;

                    if (targetRefKind != refKind)
                        diagnostics.Push(Error.ReverseRefMismatch(location, targetMethod, parameter));

                    var conversion = signatureBinder.conversions.ClassifyConversionFromType(returnType, type);

                    if (refKind != RefKind.None && !conversion.isIdentity) {
                        diagnostics.Push(Error.RefReverseMustHaveIdentityConversion(_syntax.type.location, returnType));
                    } else if (!conversion.isImplicit) {
                        Binder.GenerateImplicitConversionError(
                            diagnostics,
                            _syntax.type,
                            conversion,
                            returnType,
                            type
                        );
                    }
                }
            }
        } else {
            if (_stateMethod is not null)
                diagnostics.Push(Error.ReverseDoesNotTakeState(location, targetTypeWithAnnotations.type));

            _lazyParameters = [];
        }

        _lazyReturnType = new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void));
        CheckEffectiveAccessibility(_lazyReturnType, _lazyParameters, diagnostics);
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        ReverseClauseSyntax syntax,
        SourceMemberMethodSymbol containingMethod) {
        var hasAnyBody = syntax.body is not null;

        var declarationModifiers = MakeModifiers(containingMethod);

        var flags = new Flags(
            MethodKind.Ordinary,
            RefKind.None,
            declarationModifiers,
            true,
            true,
            hasAnyBody,
            false
        );

        return (declarationModifiers, flags);
    }

    private static DeclarationModifiers MakeModifiers(SourceMemberMethodSymbol containingMethod) {
        var declarationModifiers = containingMethod.declaredAccessibility switch {
            Accessibility.Public => DeclarationModifiers.Public,
            Accessibility.Private => DeclarationModifiers.Private,
            Accessibility.Protected => DeclarationModifiers.Protected,
            _ => DeclarationModifiers.None
        };

        if (containingMethod.isDeclaredConst)
            declarationModifiers |= DeclarationModifiers.Const;

        if (containingMethod.isLowLevel)
            declarationModifiers |= DeclarationModifiers.LowLevel;

        if (containingMethod.isStatic)
            declarationModifiers |= DeclarationModifiers.Static;

        return declarationModifiers;
    }
}
