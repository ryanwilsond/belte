using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMethodSymbol : MethodSymbol {
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
