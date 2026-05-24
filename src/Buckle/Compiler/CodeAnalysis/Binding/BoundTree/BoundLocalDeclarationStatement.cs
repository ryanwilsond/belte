using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundLocalDeclarationStatement {
    internal BoundLocalDeclarationStatement(
        SyntaxNode syntax,
        BoundDataContainerDeclaration declaration,
        bool hasErrors = false)
        : this(syntax, declaration, false, null, hasErrors) { }
}
