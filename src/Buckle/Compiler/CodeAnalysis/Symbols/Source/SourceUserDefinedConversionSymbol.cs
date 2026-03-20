using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceUserDefinedConversionSymbol : SourceUserDefinedOperatorSymbolBase {
    private SourceUserDefinedConversionSymbol(
        MethodKind methodKind,
        SourceMemberContainerTypeSymbol containingType,
        string name,
        ConversionDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics)
        : base(
            methodKind,
            name,
            containingType,
            syntax.type.location,
            syntax,
            RefKind.None,
            MakeDeclarationModifiers(syntax, syntax.operatorKeyword.location, diagnostics),
            hasAnyBody: syntax.body is not null,
            diagnostics) { }

    internal override TextLocation location => GetSyntax().operatorKeyword.location;

    private protected override TextLocation _returnTypeLocation => GetSyntax().type.location;

    internal static SourceUserDefinedConversionSymbol CreateUserDefinedConversionSymbol(
        SourceMemberContainerTypeSymbol containingType,
        ConversionDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var name = OperatorFacts.OperatorNameFromDeclaration(syntax);

        return new SourceUserDefinedConversionSymbol(
            MethodKind.Conversion,
            containingType,
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
}
