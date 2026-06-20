using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceUserDefinedOperatorSymbol : SourceUserDefinedOperatorSymbolBase {
    private SourceUserDefinedOperatorSymbol(
        MethodKind methodKind,
        SourceMemberContainerTypeSymbol containingType,
        TypeSymbol explicitInterfaceType,
        string name,
        OperatorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            methodKind,
            explicitInterfaceType,
            name,
            containingType,
            syntax.operatorToken.location,
            syntax,
            syntax.returnType.GetRefKind(),
            MakeDeclarationModifiers(containingType, methodKind, syntax, syntax.operatorToken.location, diagnostics),
            syntax.body is not null,
            diagnostics
        ) {
        location = syntax.operatorKeyword.location;
    }

    internal override TextLocation location { get; }

    private protected override TextLocation _returnTypeLocation => GetSyntax().returnType.location;

    internal static SourceUserDefinedOperatorSymbol CreateUserDefinedOperatorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        Binder bodyBinder,
        OperatorDeclarationSyntax syntax,
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
            ? MethodKind.Operator
            : MethodKind.ExplicitInterfaceImplementation;

        return new SourceUserDefinedOperatorSymbol(
            methodKind,
            containingType,
            explicitInterfaceType,
            name,
            syntax,
            diagnostics
        );
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(GetSyntax().attributeLists);
    }

    internal OperatorDeclarationSyntax GetSyntax() {
        return (OperatorDeclarationSyntax)syntaxReference.node;
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return TryGetBodyBinderFromSyntax(binderFactory, ignoreAccessibility);
    }

    private protected override int GetParameterCountFromSyntax() {
        return GetSyntax().parameterList.parameters.Count;
    }

    private protected override (TypeWithAnnotations ReturnType, ImmutableArray<ParameterSymbol> Parameters)
        MakeParametersAndBindReturnType(BelteDiagnosticQueue diagnostics) {
        var declarationSyntax = GetSyntax();
        return MakeParametersAndBindReturnType(declarationSyntax, declarationSyntax.returnType, diagnostics);
    }
}
