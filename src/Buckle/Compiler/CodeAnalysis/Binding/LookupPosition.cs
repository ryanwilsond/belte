using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal static class LookupPosition {
    internal static bool IsInMethodDeclaration(int position, BaseMethodDeclarationSyntax node) {
        var body = node.body;

        if (body is null)
            return IsBeforeToken(position, node, ((MethodDeclarationSyntax)node).semicolon);

        return IsBeforeToken(position, node, node.body.closeBrace);
    }

    private static bool IsBeforeToken(int position, BelteSyntaxNode node, SyntaxToken firstExcluded) {
        return IsBeforeToken(position, firstExcluded) && position >= node.span.start;
    }

    private static bool IsBeforeToken(int position, SyntaxToken firstExcluded) {
        return firstExcluded.kind == SyntaxKind.None || position < firstExcluded.span.start;
    }

    internal static bool IsInBody(int position, BaseMethodDeclarationSyntax method) {
        return IsInBlock(position, method.body);
    }

    internal static bool IsInBlock(int position, BlockStatementSyntax block) {
        return block is not null && IsBeforeToken(position, block, block.closeBrace);
    }

    internal static bool IsInMethodTemplateParameterScope(int position, MethodDeclarationSyntax node) {
        if (node.templateParameterList is null)
            return false;

        if (node.returnType.fullSpan.Contains(position))
            return true;

        var firstNameToken = node.identifier;
        var firstPostNameToken = node.templateParameterList.openAngleBracket;

        return !IsBetweenTokens(position, firstNameToken, firstPostNameToken);
    }

    internal static bool IsBetweenTokens(int position, SyntaxToken firstIncluded, SyntaxToken firstExcluded) {
        return position >= firstIncluded.span.start && IsBeforeToken(position, firstExcluded);
    }

    internal static bool IsInConstructorParameterScope(int position, ConstructorDeclarationSyntax node) {
        var initializerOpt = node.constructorInitializer;
        var hasBody = node.body is not null;

        if (!hasBody) {
            var nextToken = (SyntaxToken)SyntaxNavigator.Instance.GetNextToken(node, predicate: null, stepInto: null);

            return initializerOpt is null
                ? position >= node.parameterList.closeParenthesis.span.end && IsBeforeToken(position, nextToken)
                : IsBetweenTokens(position, initializerOpt.colon, nextToken);
        }

        return initializerOpt is null
            ? IsInBody(position, node)
            : IsBetweenTokens(position, initializerOpt.colon, node.body.closeBrace);
    }

    internal static bool IsInTypeDeclaration(int position, TypeDeclarationSyntax node) {
        return IsBeforeToken(position, node, node.closeBrace);
    }

    internal static bool IsInTemplateParameterList(int position, TypeDeclarationSyntax node) {
        var templateParameterList = node.templateParameterList;
        return templateParameterList is not null &&
            IsBeforeToken(position, templateParameterList, templateParameterList.closeAngleBracket);
    }
}
