using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal static class LookupPosition {
    internal static bool IsInMethodDeclaration(int position, BaseMethodDeclarationSyntax node) {
        var body = node.body;

        if (body is null)
            return IsBeforeToken(position, node, node.semicolon);

        return IsBeforeToken(position, node, node.body.closeBrace);
    }

    internal static bool IsInAttributeSpecification(
        int position,
        SyntaxList<AttributeListSyntax> attributesSyntaxList) {
        var count = attributesSyntaxList.Count;

        if (count == 0)
            return false;

        var startToken = attributesSyntaxList[0].openBracket;
        var endToken = attributesSyntaxList[count - 1].closeBracket;

        return IsBetweenTokens(position, startToken, endToken);
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

    internal static bool IsInMethodTemplateParameterScope(int position, BaseMethodDeclarationSyntax node) {
        if (node.kind == SyntaxKind.MethodDeclaration)
            return IsInMethodTemplateParameterScope(position, (MethodDeclarationSyntax)node);
        else if (node.kind == SyntaxKind.ConversionDeclaration)
            return IsInMethodTemplateParameterScope(position, (ConversionDeclarationSyntax)node);
        else if (node.kind == SyntaxKind.OperatorDeclaration)
            return IsInMethodTemplateParameterScope(position, (OperatorDeclarationSyntax)node);

        return false;
    }

    private static bool IsInMethodTemplateParameterScope(int position, MethodDeclarationSyntax node) {
        if (node.templateParameterList is null)
            return false;

        if (node.returnType.fullSpan.Contains(position))
            return true;

        var explicitInterfaceSpecifier = node.explicitInterfaceSpecifier;
        var firstNameToken = explicitInterfaceSpecifier is null
            ? node.identifier
            : explicitInterfaceSpecifier.GetFirstToken();

        var firstPostNameToken = node.templateParameterList.openAngleBracket;

        return !IsBetweenTokens(position, firstNameToken, firstPostNameToken);
    }

    private static bool IsInMethodTemplateParameterScope(int position, ConversionDeclarationSyntax node) {
        if (node.templateParameterList is null)
            return false;

        if (node.type.fullSpan.Contains(position))
            return true;

        var explicitInterfaceSpecifier = node.explicitInterfaceSpecifier;
        var firstNameToken = explicitInterfaceSpecifier is null
            ? node.operatorKeyword
            : explicitInterfaceSpecifier.GetFirstToken();

        var firstPostNameToken = node.templateParameterList.openAngleBracket;

        return !IsBetweenTokens(position, firstNameToken, firstPostNameToken);
    }

    private static bool IsInMethodTemplateParameterScope(int position, OperatorDeclarationSyntax node) {
        if (node.templateParameterList is null)
            return false;

        if (node.operatorToken.fullSpan.Contains(position) ||
            (node.rightOperatorToken is not null && node.rightOperatorToken.fullSpan.Contains(position))) {
            return true;
        }

        var explicitInterfaceSpecifier = node.explicitInterfaceSpecifier;
        var firstNameToken = explicitInterfaceSpecifier is null
            ? node.operatorKeyword
            : explicitInterfaceSpecifier.GetFirstToken();

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
        if (node is FileScopedClassDeclarationSyntax)
            return position >= node.span.start;

        return IsBeforeToken(position, node, node.closeBrace);
    }

    internal static bool IsInTemplateParameterList(int position, TypeDeclarationSyntax node) {
        var templateParameterList = node.templateParameterList;
        return templateParameterList is not null &&
            IsBeforeToken(position, templateParameterList, templateParameterList.closeAngleBracket);
    }
}
