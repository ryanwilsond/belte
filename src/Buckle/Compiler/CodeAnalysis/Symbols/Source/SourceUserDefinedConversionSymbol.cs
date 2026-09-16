using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceUserDefinedConversionSymbol : SourceUserDefinedOperatorSymbolBase {
    private TemplateParameterInfo _templateParameterInfo;

    private SourceUserDefinedConversionSymbol(
        MethodKind methodKind,
        SourceMemberContainerTypeSymbol containingType,
        TypeSymbol explicitInterfaceType,
        string name,
        ConversionDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            methodKind,
            explicitInterfaceType,
            name,
            isCompoundAssignmentOrIncrementAssignment: false,
            containingType,
            syntax.type.location,
            syntax,
            RefKind.None,
            MakeDeclarationModifiers(containingType, methodKind, syntax, syntax.operatorKeyword.location, diagnostics),
            hasAnyBody: syntax.body is not null,
            diagnostics) {
        if (isStatic && (isAbstract || isVirtual))
            ReportDefaultInterfaceImplementation(location, syntax.body is not null, diagnostics);

        var templateParameters = MakeTemplateParameters(syntax, diagnostics);
        _templateParameterInfo = templateParameters.IsEmpty
            ? TemplateParameterInfo.Empty
            : new TemplateParameterInfo { lazyTemplateParameters = templateParameters };
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters
        => _templateParameterInfo?.lazyTemplateParameters ?? [];

    public override ImmutableArray<BoundExpression> templateConstraints
        => _templateParameterInfo?.lazyTemplateConstraints ?? [];

    private protected override TextLocation _returnTypeLocation => GetSyntax().type.location;

    internal static SourceUserDefinedConversionSymbol CreateUserDefinedConversionSymbol(
        SourceMemberContainerTypeSymbol containingType,
        Binder bodyBinder,
        ConversionDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var name = SyntaxFacts.GetOperatorMemberName(syntax);
        var interfaceSpecifier = syntax.explicitInterfaceSpecifier;

        name = ExplicitInterfaceHelpers.GetMemberNameAndInterfaceSymbol(
            bodyBinder,
            syntax.modifiers,
            interfaceSpecifier,
            name,
            diagnostics,
            out var explicitInterfaceType,
            aliasQualifier: out _
        );

        var methodKind = interfaceSpecifier is null
                ? MethodKind.Conversion
                : MethodKind.ExplicitInterfaceImplementation;

        return new SourceUserDefinedConversionSymbol(
            methodKind,
            containingType,
            explicitInterfaceType,
            name,
            syntax,
            diagnostics
        );
    }

    internal ConversionDeclarationSyntax GetSyntax() {
        return (ConversionDeclarationSyntax)syntaxReference.node;
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactory, ignoreAccessibility);
    }

    private protected override int GetParameterCountFromSyntax() {
        return GetSyntax().parameterList.parameters.Count;
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(GetSyntax().attributeLists);
    }

    private protected override (TypeWithAnnotations ReturnType, ImmutableArray<ParameterSymbol> Parameters)
        MakeParametersAndBindReturnType(BelteDiagnosticQueue diagnostics) {
        var declarationSyntax = GetSyntax();
        return MakeParametersAndBindReturnType(declarationSyntax, declarationSyntax.type, diagnostics);
    }

    internal override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return GetTypeParameterConstraintTypesCore(ref _templateParameterInfo, GetSyntax());
    }

    internal override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return GetTypeParameterConstraintKindsCore(ref _templateParameterInfo, GetSyntax());
    }

    internal override ImmutableArray<BoundExpression> GetTemplateConstraints() {
        return GetTemplateConstraintsCore(ref _templateParameterInfo, GetSyntax());
    }
}
