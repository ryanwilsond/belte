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
        string name,
        OperatorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            methodKind,
            name,
            containingType,
            syntax.operatorToken.location,
            syntax,
            syntax.returnType.GetRefKind(),
            MakeDeclarationModifiers(syntax, syntax.operatorToken.location, diagnostics),
            syntax.body is not null,
            diagnostics
        ) {
        location = syntax.operatorKeyword.location;
    }

    internal override TextLocation location { get; }

    private protected override TextLocation _returnTypeLocation => GetSyntax().returnType.location;

    internal static SourceUserDefinedOperatorSymbol CreateUserDefinedOperatorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        OperatorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var name = SyntaxFacts.GetOperatorMemberName(syntax);

        return new SourceUserDefinedOperatorSymbol(
            MethodKind.Operator,
            containingType,
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
