using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceUserDefinedLiteralOperatorSymbol : SourceUserDefinedOperatorSymbolBase {
    private SourceUserDefinedLiteralOperatorSymbol(
        MethodKind methodKind,
        SourceMemberContainerTypeSymbol containingType,
        string name,
        LiteralOperatorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            methodKind,
            null,
            name,
            containingType,
            syntax.literalKeyword.location,
            syntax,
            syntax.returnType.GetRefKind(),
            MakeDeclarationModifiers(containingType, methodKind, syntax, syntax.literalKeyword.location, diagnostics),
            syntax.body is not null,
            diagnostics
        ) {
        location = syntax.literalKeyword.location;
    }

    internal override TextLocation location { get; }

    private protected override TextLocation _returnTypeLocation => GetSyntax().returnType.location;

    internal static SourceUserDefinedLiteralOperatorSymbol CreateUserDefinedLiteralOperatorSymbol(
        SourceMemberContainerTypeSymbol containingType,
        LiteralOperatorDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var name = WellKnownMemberNames.GetLiteralOperatorName(syntax.suffix.text);

        return new SourceUserDefinedLiteralOperatorSymbol(
            MethodKind.Literal,
            containingType,
            name,
            syntax,
            diagnostics
        );
    }

    internal sealed override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(GetSyntax().attributeLists);
    }

    internal LiteralOperatorDeclarationSyntax GetSyntax() {
        return (LiteralOperatorDeclarationSyntax)syntaxReference.node;
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
