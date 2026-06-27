using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceStateMethodSymbol : SourceMemberMethodSymbol {
    private readonly StateClauseSyntax _syntax;
    private readonly SourceMemberMethodSymbol _containingMethod;

    private TypeWithAnnotations _lazyReturnType;

    internal SourceStateMethodSymbol(
        StateClauseSyntax syntax,
        NamedTypeSymbol containingType,
        SourceMemberMethodSymbol containingMethod)
        : base(
            containingType,
            new SyntaxReference(syntax),
            syntax.keyword.location,
             MakeModifiersAndFlags(syntax, containingMethod)
        ) {
        _syntax = syntax;
        _containingMethod = containingMethod;
        name = GeneratedNames.MakeStateMethodName(containingMethod.name);
    }

    public override string name { get; }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    internal sealed override int parameterCount => _containingMethod.parameterCount;

    internal sealed override ImmutableArray<ParameterSymbol> parameters => _containingMethod.parameters;

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            LazyMethodChecks();
            return _lazyReturnType;
        }
    }

    internal sealed override void AfterAddingTypeMembersChecks(
        ConversionsBase conversions,
        BelteDiagnosticQueue diagnostics) {
        base.AfterAddingTypeMembersChecks(conversions, diagnostics);

        foreach (var parameter in parameters)
            parameter.type.CheckAllConstraints(conversions, parameter.syntaxReference.location, diagnostics);
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
        var t1 = _containingMethod.returnTypeWithAnnotations;

        if (_containingMethod.returnsByRef)
            diagnostics.Push(Error.ReversibleCannotBeRef(_containingMethod.location, _containingMethod));

        if (t1.IsVoidType())
            // This is purely a placeholder
            t1 = new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int32));

        var binderFactory = declaringCompilation.GetBinderFactory(_syntax.syntaxTree);

        var bodyBinder = binderFactory.GetBinder(_syntax, _syntax, this).WithContainingMember(this);
        var signatureBinder = bodyBinder
            .WithAdditionalFlagsAndContainingMember(BinderFlags.SuppressConstraintChecks, this);

        var t2 = signatureBinder.BindType(_syntax.type.SkipRef(out _), diagnostics);

        _lazyReturnType = new TypeWithAnnotations(NamedTypeSymbol.CreateTuple(
            _syntax.type.location,
            [t1, t2],
            [_containingMethod.ExtractReturnTypeSyntax().location, _syntax.type.location],
            ["ReturnValue", "CaptureValue"],
            declaringCompilation,
            false,
            default,
            _syntax.type,
            diagnostics
        ));

        CheckEffectiveAccessibility(_lazyReturnType, parameters, diagnostics);

        if (!_containingMethod.isReversible)
            diagnostics.Push(Error.StateClauseWithoutReverseClause(_containingMethod.location, _containingMethod));
    }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        StateClauseSyntax syntax,
        SourceMemberMethodSymbol containingMethod) {
        var hasAnyBody = syntax.body is not null;

        var declarationModifiers = MakeModifiers(containingMethod);

        var flags = new Flags(
            MethodKind.Ordinary,
            RefKind.None,
            declarationModifiers,
            false,
            false,
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
