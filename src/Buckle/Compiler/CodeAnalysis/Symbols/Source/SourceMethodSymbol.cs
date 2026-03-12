using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMethodSymbol : MethodSymbol {
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;
    private CustomAttributesBag<AttributeData> _lazyReturnTypeAttributesBag;

    private protected SourceMethodSymbol(SyntaxReference syntaxReference) {
        this.syntaxReference = syntaxReference;
    }

    internal sealed override bool hidesBaseMethodsByName => false;

    internal override SyntaxReference syntaxReference { get; }

    internal override bool hasSpecialName => methodKind switch {
        MethodKind.Constructor => true,
        _ => false,
    };

    internal virtual Binder outerBinder => null;

    internal virtual Binder withTemplateParametersBinder => null;

    internal BelteSyntaxNode syntaxNode => (BelteSyntaxNode)syntaxReference.node;

    internal SyntaxTree syntaxTree => syntaxReference.syntaxTree;

    internal abstract ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes();

    internal abstract ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds();

    // internal sealed override bool hasUnscopedRefAttribute => GetDecodedWellKnownAttributeData()?.hasUnscopedRefAttribute == true;
    internal sealed override bool hasUnscopedRefAttribute => false;

    private protected virtual AttributeLocation _attributeLocationForLoadAndValidateAttributes
        => AttributeLocation.None;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    internal override ImmutableArray<AttributeData> GetReturnTypeAttributes() {
        return GetReturnTypeAttributesBag().attributes;
    }

    private CustomAttributesBag<AttributeData> GetReturnTypeAttributesBag() {
        var bag = _lazyReturnTypeAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        return GetAttributesBag(ref _lazyReturnTypeAttributesBag, forReturnType: true);
    }

    internal static void ReportErrorIfHasConstraints(
        TemplateConstraintClauseListSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        if (syntax is not null && syntax.constraintClauses.Count > 0) {
            // TODO Do we even want an error here?
            // I can't imagine a situation where you could add an error-free constraint clause without having templates
            // However this would speed up compilation slightly as you wouldn't need to actually bind the constraints
            // Just push this error instead
            // EDIT: It *would* be legal to do something like `where { 3 == 3; }` and that would require no templates
        }
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        return GetAttributesBag(ref _lazyAttributesBag, forReturnType: false);
    }

    internal virtual OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(default(SyntaxList<AttributeListSyntax>));
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag(
        ref CustomAttributesBag<AttributeData> lazyAttributesBag,
        bool forReturnType) {
        var (declarations, symbolPart) = forReturnType
            ? (GetReturnTypeAttributeDeclarations(), AttributeLocation.Return)
            : (GetAttributeDeclarations(), _attributeLocationForLoadAndValidateAttributes);

        if (LoadAndValidateAttributes(
            declarations,
            ref lazyAttributesBag,
            symbolPart,
            binderOpt: outerBinder
        )) {
            NoteAttributesComplete(forReturnType);
        }

        return lazyAttributesBag;
    }

    internal virtual OneOrMany<SyntaxList<AttributeListSyntax>> GetReturnTypeAttributeDeclarations() {
        return GetAttributeDeclarations();
    }

    private protected abstract void NoteAttributesComplete(bool forReturnType);

    private protected BelteSyntaxNode GetInMethodSyntaxNode() {
        return syntaxNode switch {
            ConstructorDeclarationSyntax constructor
                => constructor.constructorInitializer ?? (BelteSyntaxNode)constructor.body,
            BaseMethodDeclarationSyntax method => method.body,
            CompilationUnitSyntax _ when this is SynthesizedEntryPoint entryPoint
                => (BelteSyntaxNode)entryPoint.returnTypeSyntax,
            LocalFunctionStatementSyntax localFunction => localFunction.body,
            ClassDeclarationSyntax classDeclaration => classDeclaration,
            _ => null,
        };
    }
}
