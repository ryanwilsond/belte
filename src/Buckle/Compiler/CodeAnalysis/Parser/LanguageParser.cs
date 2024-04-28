using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Lexes and parses text into a tree of SyntaxNodes, in doing so performing syntax checking.
/// Can optionally reuse SyntaxNodes from an old tree to speed up the parsing process.
/// </summary>
internal sealed partial class LanguageParser : SyntaxParser {
    private const int LastTerminator = (int)TerminatorState.IsEndOfTemplateArgumentList;

    private bool _expectParenthesis;
    private TerminatorState _terminatorState;
    private ParserContext _context;
    private Stack<SyntaxKind> _bracketStack;

    /// <summary>
    /// Creates a new <see cref="LanguageParser" />, requiring a fully initialized <see cref="Lexer" />.
    /// </summary>
    internal LanguageParser(Lexer lexer) : this(lexer, null, null) { }

    /// <summary>
    /// Creates a new <see cref="LanguageParser" />, requiring a fully initialized <see cref="Lexer" />.
    /// In addition, incremental parsing is enabled by passing in the previous tree and all changes.
    /// </summary>
    internal LanguageParser(Lexer lexer, SyntaxNode oldTree, IEnumerable<TextChangeRange> changes)
        : base(lexer, LexerMode.Syntax, oldTree, changes, true) {
        _expectParenthesis = false;
        _bracketStack = new Stack<SyntaxKind>();
        _bracketStack.Push(SyntaxKind.None);
    }

    /// <summary>
    /// Parses the entirety of a single file.
    /// </summary>
    /// <returns>The parsed file.</returns>
    internal CompilationUnitSyntax ParseCompilationUnit() {
        var members = ParseMembers(true);
        var endOfFile = Match(SyntaxKind.EndOfFileToken);
        return SyntaxFactory.CompilationUnit(members, endOfFile);
    }

    private new ResetPoint GetResetPoint() {
        return new ResetPoint(base.GetResetPoint(), _terminatorState, _context, _bracketStack);
    }

    private void Reset(ResetPoint resetPoint) {
        _terminatorState = resetPoint.terminatorState;
        _context = resetPoint.context;
        _bracketStack = resetPoint.bracketStack;
        Reset(resetPoint.baseResetPoint);
    }

    private SyntaxToken Match(SyntaxKind kind, SyntaxKind? nextWanted = null) {
        if (nextWanted is null && _expectParenthesis)
            nextWanted = SyntaxKind.CloseParenToken;

        return base.Match(kind, nextWanted);
    }

    private bool PeekIsOperatorDeclaration() {
        return PeekIsFunctionOrMethodOrOperatorDeclarationCore(true, true, true);
    }

    private bool PeekIsConstructorDeclaration() {
        return PeekIsFunctionOrMethodOrOperatorDeclarationCore(false, false, true);
    }

    private bool PeekIsFunctionOrMethodDeclaration(bool couldBeInStatement = true) {
        return PeekIsFunctionOrMethodOrOperatorDeclarationCore(false, true, couldBeInStatement);
    }

    private bool PeekIsFunctionOrMethodOrOperatorDeclarationCore(
        bool checkForOperator,
        bool checkForType,
        bool couldBeInStatement) {
        var hasName = false;
        var offset = 0;

        if (checkForType && !PeekIsType(0, out offset, out hasName, out _))
            return false;

        if (!checkForType && !checkForOperator && Peek(offset).kind == SyntaxKind.IdentifierToken)
            hasName = true;

        if (checkForOperator) {
            if (Peek(offset).kind == SyntaxKind.OperatorKeyword)
                offset++;
            else
                return false;
        }

        if (checkForOperator && Peek(offset).kind.IsOverloadableOperator()) {
            if (Peek(offset).kind == SyntaxKind.OpenBracketToken &&
                Peek(offset + 1).kind == SyntaxKind.CloseBracketToken) {
                offset++;
            }

            hasName = true;
        } else if (checkForOperator && Peek(offset).kind != SyntaxKind.OpenParenToken) {
            offset++;
        }

        if (hasName)
            offset++;

        if (Peek(offset).kind != SyntaxKind.OpenParenToken)
            return false;

        if (!couldBeInStatement)
            return true;

        // If we get here it means that we are inside of a statement and if we do decide this is a function or method,
        // it is a local function. This logic is to make sure we don't accidentally treat a call as a function.
        var parenthesisStack = 0;

        while (Peek(offset).kind != SyntaxKind.EndOfFileToken) {
            if (Peek(offset).kind == SyntaxKind.OpenParenToken)
                parenthesisStack++;
            else if (Peek(offset).kind == SyntaxKind.CloseParenToken)
                parenthesisStack--;

            if (Peek(offset).kind == SyntaxKind.CloseParenToken && parenthesisStack == 0) {
                if (Peek(offset + 1).kind == SyntaxKind.OpenBraceToken)
                    return true;
                else
                    return false;
            } else {
                offset++;
            }
        }

        return false;
    }

    private bool PeekIsLocalDeclaration() {
        var offset = 0;

        while (Peek(offset).kind == SyntaxKind.OpenBracketToken) {
            offset++;

            while (Peek(offset).kind is SyntaxKind.IdentifierToken or SyntaxKind.CommaToken)
                offset++;

            if (Peek(offset).kind == SyntaxKind.CloseBracketToken)
                offset++;
        }

        var hasConstKeyword = false;

        while (Peek(offset).kind is SyntaxKind.ConstexprKeyword or SyntaxKind.ConstKeyword) {
            offset++;
            hasConstKeyword = true;
        }

        return PeekIsType(offset, out _, out var hasName, out _) && (hasName || hasConstKeyword);
    }

    private bool PeekIsType(int offset, out int finalOffset, out bool hasName, out bool isTemplate) {
        finalOffset = offset;
        hasName = false;
        isTemplate = false;

        if (Peek(finalOffset).kind is not SyntaxKind.IdentifierToken and not SyntaxKind.RefKeyword)
            return false;

        while (Peek(finalOffset).kind is SyntaxKind.ConstKeyword or SyntaxKind.RefKeyword)
            finalOffset++;

        if (Peek(finalOffset).kind is not SyntaxKind.IdentifierToken &&
            Peek(finalOffset - 1).kind is not SyntaxKind.ConstKeyword and not SyntaxKind.ConstexprKeyword) {
            return false;
        }

        if (Peek(finalOffset).kind is SyntaxKind.IdentifierToken)
            finalOffset++;

        while (Peek(finalOffset).kind == SyntaxKind.LessThanToken) {
            isTemplate = true;
            finalOffset++;

            while (Peek(finalOffset).kind is not SyntaxKind.GreaterThanToken and not SyntaxKind.EndOfFileToken)
                finalOffset++;

            finalOffset++;
        }

        var hasBrackets = false;
        var bracketsBeenClosed = true;

        while (Peek(finalOffset).kind is SyntaxKind.OpenBracketToken or SyntaxKind.CloseBracketToken) {
            hasBrackets = true;

            if (Peek(finalOffset).kind is SyntaxKind.OpenBracketToken)
                bracketsBeenClosed = false;
            if (Peek(finalOffset).kind is SyntaxKind.CloseBracketToken)
                bracketsBeenClosed = true;

            finalOffset++;
        }

        if (Peek(finalOffset).kind is SyntaxKind.ExclamationToken)
            finalOffset++;

        if (Peek(finalOffset).kind is SyntaxKind.IdentifierToken && bracketsBeenClosed)
            hasName = true;

        if (!hasBrackets &&
            Peek(finalOffset).kind != SyntaxKind.IdentifierToken &&
            Peek(finalOffset - 2).kind is SyntaxKind.ConstKeyword or SyntaxKind.ConstexprKeyword &&
            Peek(finalOffset - 1).kind == SyntaxKind.IdentifierToken) {
            hasName = true;
            finalOffset--;
        }

        return true;
    }

    private bool PeekIsCastExpression() {
        if (currentToken.kind == SyntaxKind.OpenParenToken &&
            PeekIsType(1, out var offset, out _, out _) &&
            Peek(offset).kind == SyntaxKind.CloseParenToken) {
            if (Peek(offset + 1).kind == SyntaxKind.OpenParenToken)
                return true;

            var isBinary = Peek(offset + 1).kind.GetBinaryPrecedence() > 0;
            var isUnary = Peek(offset + 1).kind.GetBinaryPrecedence() > 0;
            var isTernary = Peek(offset + 1).kind.GetTernaryPrecedence() > 0;
            var isPrimary = Peek(offset + 1).kind.GetPrimaryPrecedence() > 0;
            var isEquals = Peek(offset + 1).kind == SyntaxKind.EqualsToken;

            if (!isBinary && !isUnary && !isTernary && !isPrimary && !isEquals)
                return true;
        }

        return false;
    }

    private bool IsTerminator() {
        if (currentToken.kind == SyntaxKind.EndOfFileToken)
            return true;

        if (currentToken.kind == _bracketStack.Peek())
            return true;

        for (var i = 1; i < LastTerminator; i <<= 1) {
            switch (_terminatorState & (TerminatorState)i) {
                case TerminatorState.IsEndOfTemplateParameterList when IsEndOfTemplateParameterList():
                case TerminatorState.IsEndOfTemplateArgumentList when IsEndOfTemplateArgumentList():
                    return true;
            }
        }

        return false;
    }

    private bool IsEndOfTemplateParameterList() => currentToken.kind == SyntaxKind.GreaterThanToken;

    private bool IsEndOfTemplateArgumentList() => currentToken.kind == SyntaxKind.GreaterThanToken;

    private SyntaxList<MemberDeclarationSyntax> ParseMembers(bool isGlobal = false) {
        var members = SyntaxListBuilder<MemberDeclarationSyntax>.Create();

        while (currentToken.kind != SyntaxKind.EndOfFileToken) {
            if (!isGlobal && currentToken.kind == SyntaxKind.CloseBraceToken)
                break;

            var startToken = currentToken;

            var member = ParseMember(isGlobal);
            members.Add(member);

            if (currentToken == startToken)
                EatToken();
        }

        return members.ToList();
    }

    private bool TryParseMember(bool allowGlobalStatements, out MemberDeclarationSyntax member) {
        if (currentToken.kind == SyntaxKind.BadToken) {
            member = null;
            return false;
        }

        member = ParseMember(allowGlobalStatements);
        return true;
    }

    private MemberDeclarationSyntax ParseMember(bool allowGlobalStatements = false) {
        var attributeLists = ParseAttributeLists();
        var modifiers = ParseModifiers();

        if ((_context & ParserContext.InClassDefinition) != 0 && PeekIsConstructorDeclaration())
            return ParseConstructorDeclaration(attributeLists, modifiers);

        if ((_context & ParserContext.InClassDefinition) != 0 && PeekIsOperatorDeclaration())
            return ParseOperatorDeclaration(attributeLists, modifiers);

        if (PeekIsFunctionOrMethodDeclaration(couldBeInStatement: allowGlobalStatements))
            return ParseMethodDeclaration(attributeLists, modifiers);

        switch (currentToken.kind) {
            case SyntaxKind.StructKeyword:
                return ParseStructDeclaration(attributeLists, modifiers);
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration(attributeLists, modifiers);
            default:
                if (allowGlobalStatements) {
                    if (attributeLists.Any()) {
                        var builder = new SyntaxListBuilder<AttributeListSyntax>(attributeLists.Count);

                        for (var i = 0; i < attributeLists.Count; i++) {
                            if (i == 0)
                                builder.Add(AddDiagnostic(attributeLists[i], Error.InvalidAttributes()));
                            else
                                builder.Add(attributeLists[i]);
                        }

                        attributeLists = builder.ToList();
                    }

                    if (modifiers.Any()) {
                        var builder = new SyntaxListBuilder<SyntaxToken>(modifiers.Count);

                        foreach (var modifier in modifiers) {
                            if (modifier.kind is SyntaxKind.ConstKeyword or SyntaxKind.ConstexprKeyword) {
                                builder.Add(modifier);
                                continue;
                            }

                            builder.Add(
                                AddDiagnostic(modifier, Error.InvalidModifier(SyntaxFacts.GetText(modifier.kind)))
                            );
                        }

                        modifiers = builder.ToList();
                    }

                    return ParseGlobalStatement(attributeLists, modifiers);
                } else {
                    return ParseFieldDeclaration(attributeLists, modifiers);
                }
        }
    }

    private MemberDeclarationSyntax ParseStructDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        TemplateParameterListSyntax templateParameterList = null;

        if (currentToken.kind == SyntaxKind.LessThanToken)
            templateParameterList = ParseTemplateParameterList();

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var saved = _context;
        _context |= ParserContext.InStructDefinition;
        var members = ParseFieldList();
        _context = saved;
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.StructDeclaration(
            attributeLists,
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            openBrace,
            members,
            closeBrace
        );
    }

    private MemberDeclarationSyntax ParseClassDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        TemplateParameterListSyntax templateParameterList = null;
        SyntaxToken openBrace = null;
        SyntaxList<MemberDeclarationSyntax> members = null;
        SyntaxToken closeBrace = null;
        SyntaxToken semicolon = null;
        var containsBody = false;

        if (currentToken.kind == SyntaxKind.LessThanToken)
            templateParameterList = ParseTemplateParameterList();

        if (currentToken.kind == SyntaxKind.OpenBraceToken) {
            openBrace = Match(SyntaxKind.OpenBraceToken);
            var saved = _context;
            _context |= ParserContext.InClassDefinition;
            members = ParseMembers();
            _context = saved;
            closeBrace = Match(SyntaxKind.CloseBraceToken);
            containsBody = true;
        }

        if (currentToken.kind == SyntaxKind.SemicolonToken || !containsBody)
            semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ClassDeclaration(
            attributeLists,
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            openBrace,
            members,
            closeBrace,
            semicolon
        );
    }

    private ConstructorDeclarationSyntax ParseConstructorDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.ConstructorDeclaration(attributeLists, modifiers, identifier, parameterList, body);
    }

    private MemberDeclarationSyntax ParseMethodDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.MethodDeclaration(attributeLists, modifiers, type, identifier, parameterList, body);
    }

    private OperatorDeclarationSyntax ParseOperatorDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var type = ParseType(false);
        var operatorKeyword = Match(SyntaxKind.OperatorKeyword);
        var operatorToken = EatToken();
        SyntaxToken rightOperatorToken = null;

        if (!operatorToken.kind.IsOverloadableOperator())
            operatorToken = AddDiagnostic(operatorToken, Error.ExpectedOverloadableOperator());

        if (operatorToken.kind == SyntaxKind.OpenBracketToken)
            rightOperatorToken = Match(SyntaxKind.CloseBracketToken);

        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.OperatorDeclaration(
            attributeLists,
            modifiers,
            type,
            operatorKeyword,
            operatorToken,
            rightOperatorToken,
            parameterList,
            body
        );
    }

    private StatementSyntax ParseLocalFunctionDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        attributeLists ??= ParseAttributeLists();
        modifiers ??= ParseModifiers();
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);
        var parameters = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.LocalFunctionStatement(
            attributeLists,
            modifiers,
            type,
            identifier,
            parameters,
            body
        );
    }

    private DeclarationModifiers GetModifier(SyntaxToken token) {
        return token.kind switch {
            SyntaxKind.StaticKeyword => DeclarationModifiers.Static,
            SyntaxKind.ConstKeyword => DeclarationModifiers.Const,
            SyntaxKind.ConstexprKeyword => DeclarationModifiers.Constexpr,
            _ => DeclarationModifiers.None,
        };
    }

    private SyntaxList<SyntaxToken> ParseModifiers() {
        var modifiers = SyntaxListBuilder<SyntaxToken>.Create();

        while (true) {
            var modifier = GetModifier(currentToken);

            if (modifier == DeclarationModifiers.None)
                break;

            modifiers.Add(EatToken());
        }

        return modifiers.ToList();
    }

    private ParameterListSyntax ParseParameterList() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameters();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ParameterList(openParenthesis, parameters, closeParenthesis);
    }

    private TemplateParameterListSyntax ParseTemplateParameterList() {
        var openAngleBracket = Match(SyntaxKind.LessThanToken);

        _bracketStack.Push(SyntaxKind.GreaterThanToken);
        var saved = _terminatorState;
        _terminatorState |= TerminatorState.IsEndOfTemplateParameterList;
        var parameters = ParseParameters();
        _terminatorState = saved;
        _bracketStack.Pop();

        var closeAngleBracket = Match(SyntaxKind.GreaterThanToken);

        return SyntaxFactory.TemplateParameterList(openAngleBracket, parameters, closeAngleBracket);
    }

    private SeparatedSyntaxList<ParameterSyntax> ParseParameters() {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextParameter = true;
        var saved = _context;
        _context |= ParserContext.InExpression;

        while (parseNextParameter &&
            currentToken.kind != SyntaxKind.CloseParenToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            var expression = ParseParameter();
            nodesAndSeparators.Add(expression);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextParameter = false;
            }
        }

        _context = saved;

        return new SeparatedSyntaxList<ParameterSyntax>(nodesAndSeparators.ToList());
    }

    private ParameterSyntax ParseParameter() {
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);

        SyntaxToken equals = null;
        ExpressionSyntax defaultValue = null;

        if (currentToken.kind == SyntaxKind.EqualsToken) {
            equals = EatToken();
            defaultValue = ParseNonAssignmentExpression();
        }

        return SyntaxFactory.Parameter(type, identifier, equals, defaultValue);
    }

    private SyntaxList<MemberDeclarationSyntax> ParseFieldList() {
        var fieldDeclarations = SyntaxListBuilder<MemberDeclarationSyntax>.Create();

        while (currentToken.kind is not SyntaxKind.CloseBraceToken and not SyntaxKind.EndOfFileToken) {
            var attributeLists = ParseAttributeLists();
            var modifiers = ParseModifiers();
            var field = ParseFieldDeclaration(attributeLists, modifiers);
            fieldDeclarations.Add(field);
        }

        return fieldDeclarations.ToList();
    }

    private FieldDeclarationSyntax ParseFieldDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var declaration = ParseVariableDeclaration(false);
        var semicolon = Match(SyntaxKind.SemicolonToken);
        return SyntaxFactory.FieldDeclaration(attributeLists, modifiers, declaration, semicolon);
    }

    private MemberDeclarationSyntax ParseGlobalStatement(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var statement = ParseStatementCore(
            attributeLists,
            modifiers,
            out var consumedAttributeLists,
            out var consumedModifiers
        );

        if (consumedAttributeLists)
            attributeLists = null;
        if (consumedModifiers)
            modifiers = null;

        return SyntaxFactory.GlobalStatement(attributeLists, modifiers, statement);
    }

    private VariableDeclarationSyntax ParseVariableDeclaration(
        bool allowImplicit = true,
        bool hasConstKeyword = false) {
        var inStruct = (_context & ParserContext.InStructDefinition) != 0;
        var type = ParseType(allowImplicit: allowImplicit, allowRef: !inStruct, hasConstKeyword: hasConstKeyword);
        var identifier = Match(SyntaxKind.IdentifierToken);
        EqualsValueClauseSyntax initializer = null;

        if (currentToken.kind == SyntaxKind.EqualsToken)
            initializer = ParseEqualsValueClause(inStruct);

        return SyntaxFactory.VariableDeclaration(type, identifier, initializer);
    }

    private EqualsValueClauseSyntax ParseEqualsValueClause(bool inStruct) {
        var equals = EatToken();
        var value = ParseExpression();

        if (inStruct)
            equals = AddDiagnostic(equals, Error.CannotInitializeInStructs());

        return SyntaxFactory.EqualsValueClause(equals, value);
    }

    private StatementSyntax ParseStatement() {
        return ParseStatementCore(null, null, out _, out _);
    }

    private StatementSyntax ParseStatementCore(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        out bool consumedAttributeLists,
        out bool consumedModifiers) {
        var saved = _context;
        _context |= ParserContext.InStatement;

        var statement = ParseStatementInternal(
            attributeLists,
            modifiers,
            out consumedAttributeLists,
            out consumedModifiers
        );

        _context = saved;

        return statement;
    }

    private StatementSyntax ParseStatementInternal(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        out bool consumedAttributeLists,
        out bool consumedModifiers) {
        consumedAttributeLists = false;
        consumedModifiers = false;

        switch (currentToken.kind) {
            case SyntaxKind.OpenBraceToken:
                return ParseBlockStatement();
            case SyntaxKind.IfKeyword:
                return ParseIfStatement();
            case SyntaxKind.WhileKeyword:
                return ParseWhileStatement();
            case SyntaxKind.ForKeyword:
                return ParseForStatement();
            case SyntaxKind.DoKeyword:
                return ParseDoWhileStatement();
            case SyntaxKind.TryKeyword:
                return ParseTryStatement();
            case SyntaxKind.BreakKeyword:
                return ParseBreakStatement();
            case SyntaxKind.ContinueKeyword:
                return ParseContinueStatement();
            case SyntaxKind.ReturnKeyword:
                return ParseReturnStatement();
        }

        if (PeekIsFunctionOrMethodDeclaration()) {
            consumedAttributeLists = true;
            consumedModifiers = true;
            return ParseLocalFunctionDeclaration(attributeLists, modifiers);
        }

        if (PeekIsLocalDeclaration()) {
            consumedAttributeLists = true;
            consumedModifiers = true;
            return ParseLocalDeclarationStatement(attributeLists, modifiers);
        }

        return ParseExpressionStatement();
    }

    private StatementSyntax ParseLocalDeclarationStatement(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        attributeLists ??= ParseAttributeLists();
        modifiers ??= ParseModifiers();
        var hasConstKeyword = false;

        foreach (var modifier in modifiers) {
            if (modifier.kind is SyntaxKind.ConstKeyword or SyntaxKind.ConstexprKeyword) {
                hasConstKeyword = true;
                break;
            }
        }

        var declaration = ParseVariableDeclaration(hasConstKeyword: hasConstKeyword);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.LocalDeclarationStatement(attributeLists, modifiers, declaration, semicolon);
    }

    private StatementSyntax ParseTryStatement() {
        var keyword = EatToken();
        var body = (BlockStatementSyntax)ParseBlockStatement();
        var catchClause = ParseCatchClause();
        var finallyClause = ParseFinallyClause();

        if (catchClause is null && finallyClause is null) {
            body = AddDiagnostic(
                body,
                Error.NoCatchOrFinally(),
                body.GetSlotOffset(2) + body.closeBrace.GetLeadingTriviaWidth(),
                body.closeBrace.width
            );
        }

        return SyntaxFactory.TryStatement(keyword, body, catchClause, finallyClause);
    }

    private CatchClauseSyntax ParseCatchClause() {
        if (currentToken.kind != SyntaxKind.CatchKeyword)
            return null;

        var keyword = EatToken();
        var body = ParseBlockStatement();

        return SyntaxFactory.CatchClause(keyword, (BlockStatementSyntax)body);
    }

    private FinallyClauseSyntax ParseFinallyClause() {
        if (currentToken.kind != SyntaxKind.FinallyKeyword)
            return null;

        var keyword = EatToken();
        var body = ParseBlockStatement();

        return SyntaxFactory.FinallyClause(keyword, (BlockStatementSyntax)body);
    }

    private StatementSyntax ParseReturnStatement() {
        var keyword = EatToken();
        ExpressionSyntax expression = null;

        if (currentToken.kind != SyntaxKind.SemicolonToken)
            expression = ParseExpression();

        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ReturnStatement(keyword, expression, semicolon);
    }

    private StatementSyntax ParseContinueStatement() {
        var keyword = EatToken();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ContinueStatement(keyword, semicolon);
    }

    private StatementSyntax ParseBreakStatement() {
        var keyword = EatToken();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.BreakStatement(keyword, semicolon);
    }

    private StatementSyntax ParseDoWhileStatement() {
        var doKeyword = EatToken();
        var body = ParseStatement();
        var whileKeyword = Match(SyntaxKind.WhileKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.DoWhileStatement(
            doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon
        );
    }

    private StatementSyntax ParseWhileStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return SyntaxFactory.WhileStatement(keyword, openParenthesis, condition, closeParenthesis, body);
    }

    private StatementSyntax ParseForStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);

        var initializer = ParseStatement();
        var condition = ParseNonAssignmentExpression();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        ExpressionSyntax step;
        if (currentToken.kind == SyntaxKind.CloseParenToken)
            step = SyntaxFactory.Empty();
        else
            step = ParseExpression();

        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return SyntaxFactory.ForStatement(
            keyword, openParenthesis, initializer, condition, semicolon, step, closeParenthesis, body
        );
    }

    private StatementSyntax ParseIfStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var condition = ParseNonAssignmentExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var then = ParseStatement();

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
        var nestedIf = false;
        var inner = then;
        var offset = 0;

        while (inner.kind == SyntaxKind.IfStatement) {
            nestedIf = true;
            var innerIf = (IfStatementSyntax)inner;
            offset += innerIf.GetSlotOffset(4);

            if (innerIf.elseClause != null && innerIf.then.kind != SyntaxKind.BlockStatement) {
                var elseOffset = offset + innerIf.then.fullWidth + innerIf.elseClause.GetLeadingTriviaWidth();

                then = AddDiagnostic(
                    then,
                    Error.AmbiguousElse(),
                    elseOffset,
                    innerIf.elseClause.keyword.width
                );
            }

            if (innerIf.then.kind == SyntaxKind.IfStatement)
                inner = innerIf.then;
            else
                break;
        }

        var elseClause = ParseElseClause();

        if (elseClause != null && then.kind != SyntaxKind.BlockStatement && nestedIf) {
            elseClause = AddDiagnostic(
                elseClause,
                Error.AmbiguousElse(),
                elseClause.keyword.GetLeadingTriviaWidth(),
                elseClause.keyword.width
            );
        }

        return SyntaxFactory.IfStatement(keyword, openParenthesis, condition, closeParenthesis, then, elseClause);
    }

    private ElseClauseSyntax ParseElseClause() {
        if (currentToken.kind != SyntaxKind.ElseKeyword)
            return null;

        var keyword = Match(SyntaxKind.ElseKeyword);
        var statement = ParseStatement();

        return SyntaxFactory.ElseClause(keyword, statement);
    }

    private StatementSyntax ParseExpressionStatement() {
        var diagnosticCount = currentToken.GetDiagnostics().Length;
        var expression = ParseExpression(allowEmpty: true);
        var nextDiagnosticCount = currentToken.GetDiagnostics().Length;
        var semicolon = Match(SyntaxKind.SemicolonToken);

        if (nextDiagnosticCount > diagnosticCount && semicolon.containsDiagnostics) {
            var diagnostics = semicolon.GetDiagnostics();

            if (!semicolon.isFabricated)
                semicolon = semicolon.WithDiagnosticsGreen(diagnostics);
            else
                semicolon = semicolon.WithDiagnosticsGreen(diagnostics.SkipLast(1).ToArray());
        }

        return SyntaxFactory.ExpressionStatement(expression, semicolon);
    }

    private StatementSyntax ParseBlockStatement() {
        var statements = SyntaxListBuilder<StatementSyntax>.Create();
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var startToken = currentToken;

        while (currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        }

        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.BlockStatement(openBrace, statements.ToList(), closeBrace);
    }

    private ExpressionSyntax ParseAssignmentExpression() {
        var left = ParseOperatorExpression();

        switch (currentToken.kind) {
            case SyntaxKind.PlusEqualsToken:
            case SyntaxKind.MinusEqualsToken:
            case SyntaxKind.AsteriskEqualsToken:
            case SyntaxKind.SlashEqualsToken:
            case SyntaxKind.AmpersandEqualsToken:
            case SyntaxKind.PipeEqualsToken:
            case SyntaxKind.AsteriskAsteriskEqualsToken:
            case SyntaxKind.CaretEqualsToken:
            case SyntaxKind.LessThanLessThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
            case SyntaxKind.PercentEqualsToken:
            case SyntaxKind.QuestionQuestionEqualsToken:
            case SyntaxKind.EqualsToken:
                var operatorToken = EatToken();
                var right = ParseAssignmentExpression();
                left = SyntaxFactory.AssignmentExpression(left, operatorToken, right);
                break;
            default:
                break;
        }

        return left;
    }

    private ExpressionSyntax ParseNonAssignmentExpression() {
        var saved = _context;
        _context |= ParserContext.InExpression;

        if (currentToken.kind == SyntaxKind.SemicolonToken)
            return ParseEmptyExpression();

        _expectParenthesis = true;
        var value = ParseOperatorExpression();
        _expectParenthesis = false;
        _context = saved;
        return value;
    }

    private ExpressionSyntax ParseExpression(bool allowEmpty = false) {
        var saved = _context;
        _context |= ParserContext.InExpression;

        if (currentToken.kind == SyntaxKind.SemicolonToken) {
            if (!allowEmpty)
                AddDiagnosticToNextToken(Error.ExpectedToken(SyntaxKind.IdentifierName));

            return ParseEmptyExpression();
        }

        var expression = ParseAssignmentExpression();
        _context = saved;
        return expression;
    }

    private ExpressionSyntax ParseEmptyExpression() {
        return SyntaxFactory.Empty();
    }

    private ExpressionSyntax ParseOperatorExpression(int parentPrecedence = 0) {
        ExpressionSyntax left;
        var unaryPrecedence = currentToken.kind.GetUnaryPrecedence();

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence && !IsTerminator()) {
            var operatorToken = EatToken();

            if (operatorToken.kind is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken) {
                var operand = ParsePrimaryExpression();
                left = SyntaxFactory.PrefixExpression(operatorToken, operand);
            } else {
                var operand = ParseOperatorExpression(unaryPrecedence);
                left = SyntaxFactory.UnaryExpression(operatorToken, operand);
            }
        } else {
            left = ParsePrimaryExpression();
        }

        while (true) {
            var tokensToCombine = 1;
            var precedence = currentToken.kind.GetBinaryPrecedence();

            if (currentToken.kind == SyntaxKind.GreaterThanToken &&
                Peek(1).kind == SyntaxKind.GreaterThanToken &&
                NoTriviaBetween(currentToken, Peek(1))) {
                if (Peek(2).kind == SyntaxKind.GreaterThanToken && NoTriviaBetween(Peek(1), Peek(2))) {
                    tokensToCombine = 3;
                    precedence = SyntaxKind.GreaterThanGreaterThanGreaterThanToken.GetBinaryPrecedence();
                } else {
                    tokensToCombine = 2;
                    precedence = SyntaxKind.GreaterThanGreaterThanToken.GetBinaryPrecedence();
                }
            }

            if (precedence == 0 || precedence <= parentPrecedence || IsTerminator())
                break;

            var operatorToken = EatToken();

            if (tokensToCombine == 2) {
                var operatorToken2 = EatToken();

                operatorToken = SyntaxFactory.Token(
                    operatorToken.GetLeadingTrivia(),
                    SyntaxKind.GreaterThanGreaterThanToken,
                    operatorToken2.GetTrailingTrivia()
                );
            } else if (tokensToCombine == 3) {
                EatToken();
                var operatorToken2 = EatToken();

                operatorToken = SyntaxFactory.Token(
                    operatorToken.GetLeadingTrivia(),
                    SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
                    operatorToken2.GetTrailingTrivia()
                );
            } else if (tokensToCombine != 1) {
                throw ExceptionUtilities.Unreachable();
            }

            var right = ParseOperatorExpression(precedence);
            left = SyntaxFactory.BinaryExpression(left, operatorToken, right);
        }

        while (true) {
            var precedence = currentToken.kind.GetTernaryPrecedence();

            if (precedence == 0 || precedence < parentPrecedence || IsTerminator())
                break;

            var leftOperatorToken = EatToken();
            var center = ParseOperatorExpression(precedence);
            var rightOperatorToken = Match(leftOperatorToken.kind.GetTernaryOperatorPair());
            var right = ParseOperatorExpression(precedence);
            left = SyntaxFactory.TernaryExpression(left, leftOperatorToken, center, rightOperatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpressionInternal() {
        switch (currentToken.kind) {
            case SyntaxKind.OpenParenToken:
                if (PeekIsCastExpression())
                    return ParseCastExpression();
                else
                    return ParseParenthesizedExpression();
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
                return ParseBooleanLiteral();
            case SyntaxKind.NumericLiteralToken:
                return ParseNumericLiteral();
            case SyntaxKind.StringLiteralToken:
                return ParseStringLiteral();
            case SyntaxKind.NullKeyword:
                return ParseNullLiteral();
            case SyntaxKind.OpenBraceToken:
                return ParseInitializerListExpression();
            case SyntaxKind.RefKeyword:
                return ParseReferenceExpression();
            case SyntaxKind.TypeOfKeyword:
                return ParseTypeOfExpression();
            case SyntaxKind.NewKeyword:
                return ParseObjectCreationExpression();
            case SyntaxKind.ThisKeyword:
                return ParseThisExpression();
            case SyntaxKind.IdentifierToken:
            default:
                return ParseLastCaseName();
        }
    }

    private ExpressionSyntax ParsePrimaryExpression(int parentPrecedence = 0, ExpressionSyntax left = null) {
        ExpressionSyntax ParseCorrectPrimaryOperator(ExpressionSyntax expression) {
            return currentToken.kind switch {
                SyntaxKind.OpenParenToken => ParseCallExpression(expression),
                SyntaxKind.OpenBracketToken or SyntaxKind.QuestionOpenBracketToken => ParseIndexExpression(expression),
                SyntaxKind.PeriodToken or SyntaxKind.QuestionPeriodToken => ParseMemberAccessExpression(expression),
                SyntaxKind.MinusMinusToken or SyntaxKind.PlusPlusToken or SyntaxKind.ExclamationToken => ParsePostfixExpression(expression),
                _ => expression,
            };
        }

        left ??= ParsePrimaryExpressionInternal();

        while (true) {
            var startToken = currentToken;
            var precedence = currentToken.kind.GetPrimaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            left = ParseCorrectPrimaryOperator(left);
            left = ParsePrimaryExpression(precedence, left);

            if (startToken == currentToken)
                EatToken();
        }

        return left;
    }

    private ExpressionSyntax ParseCastExpression() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false, false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var expression = ParseExpression();

        return SyntaxFactory.CastExpression(openParenthesis, type, closeParenthesis, expression);
    }

    private ExpressionSyntax ParseReferenceExpression() {
        var keyword = Match(SyntaxKind.RefKeyword);
        var expression = ParseExpression();

        return SyntaxFactory.ReferenceExpression(keyword, expression);
    }

    private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax operand) {
        var operatorToken = EatToken();
        return SyntaxFactory.PostfixExpression(operand, operatorToken);
    }

    private ExpressionSyntax ParseInitializerListExpression() {
        var left = Match(SyntaxKind.OpenBraceToken);
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextItem = true;

        while (parseNextItem && currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            if (currentToken.kind is not SyntaxKind.CommaToken and not SyntaxKind.CloseBraceToken) {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);
            } else {
                var empty = SyntaxFactory.Empty();
                nodesAndSeparators.Add(empty);
            }

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToList());
        var right = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.InitializerListExpression(left, separatedSyntaxList, right);
    }

    private ExpressionSyntax ParseParenthesizedExpression() {
        var left = Match(SyntaxKind.OpenParenToken);
        _bracketStack.Push(SyntaxKind.CloseParenToken);
        var expression = ParseExpression();
        _bracketStack.Pop();
        var right = Match(SyntaxKind.CloseParenToken);


        return SyntaxFactory.ParenthesisExpression(left, expression, right);
    }

    private ExpressionSyntax ParseTypeOfExpression() {
        var keyword = Match(SyntaxKind.TypeOfKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.TypeOfExpression(keyword, openParenthesis, type, closeParenthesis);
    }

    private ExpressionSyntax ParseObjectCreationExpression() {
        var keyword = Match(SyntaxKind.NewKeyword);
        var type = ParseType(allowImplicit: false, allowArraySize: true);
        ArgumentListSyntax argumentList = null;

        bool IsArrayType(TypeSyntax syntax) {
            if (syntax is ArrayTypeSyntax)
                return true;
            else if (syntax is NonNullableTypeSyntax n)
                return IsArrayType(n.type);
            else if (syntax is ReferenceTypeSyntax r)
                return IsArrayType(r.type);
            else
                return false;
        }

        if (!IsArrayType(type))
            argumentList = ParseArgumentList();

        return SyntaxFactory.ObjectCreationExpression(keyword, type, argumentList);
    }

    private ExpressionSyntax ParseThisExpression() {
        var keyword = Match(SyntaxKind.ThisKeyword);
        return SyntaxFactory.ThisExpression(keyword);
    }

    private ExpressionSyntax ParseMemberAccessExpression(ExpressionSyntax expression) {
        var operatorToken = EatToken();
        var name = ParseSimpleName();

        return SyntaxFactory.MemberAccessExpression(expression, operatorToken, name);
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax expression) {
        var openBracket = EatToken();
        _bracketStack.Push(SyntaxKind.CloseBracketToken);
        var index = ParseExpression();
        _bracketStack.Pop();
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.IndexExpression(expression, openBracket, index, closeBracket);
    }

    private ScanTemplateArgumentListKind ScanTemplateArgumentList() {
        if (currentToken.kind != SyntaxKind.LessThanToken)
            return ScanTemplateArgumentListKind.NotTemplateArgumentList;

        if ((_context & ParserContext.InExpression) == 0)
            return ScanTemplateArgumentListKind.DefiniteTemplateArgumentList;

        var lookahead = 1;

        while (Peek(lookahead).kind is not SyntaxKind.GreaterThanToken and not SyntaxKind.EndOfFileToken)
            lookahead++;

        return Peek(lookahead + 1).kind switch {
            SyntaxKind.OpenParenToken or SyntaxKind.EndOfFileToken => ScanTemplateArgumentListKind.PossibleTemplateArgumentList,
            _ => ScanTemplateArgumentListKind.NotTemplateArgumentList,
        };
    }

    private ExpressionSyntax ParseCallExpression(ExpressionSyntax expression) {
        var argumentList = ParseArgumentList();
        return SyntaxFactory.CallExpression(expression, argumentList);
    }

    private TemplateArgumentListSyntax ParseTemplateArgumentList() {
        var openAngleBracket = Match(SyntaxKind.LessThanToken);

        _bracketStack.Push(SyntaxKind.GreaterThanToken);
        var savedTerminatorState = _terminatorState;
        var savedContext = _context;
        _terminatorState |= TerminatorState.IsEndOfTemplateArgumentList;
        _context |= ParserContext.InTemplateArgumentList;
        var arguments = ParseArguments(SyntaxKind.GreaterThanToken);
        _terminatorState = savedTerminatorState;
        _context = savedContext;
        _bracketStack.Pop();

        var closeAngleBracket = Match(SyntaxKind.GreaterThanToken);

        return SyntaxFactory.TemplateArgumentList(openAngleBracket, arguments, closeAngleBracket);
    }

    private ArgumentListSyntax ParseArgumentList() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var arguments = ParseArguments(SyntaxKind.CloseParenToken);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ArgumentList(openParenthesis, arguments, closeParenthesis);
    }

    private SeparatedSyntaxList<ArgumentSyntax> ParseArguments(SyntaxKind closeBracket) {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextArgument = true;

        if (currentToken.kind != SyntaxKind.CloseParenToken) {
            while (parseNextArgument && currentToken.kind != SyntaxKind.EndOfFileToken) {
                if (currentToken.kind != SyntaxKind.CommaToken && currentToken.kind != closeBracket) {
                    var argument = ParseArgument();
                    nodesAndSeparators.Add(argument);
                } else {
                    var empty = SyntaxFactory.Argument(
                        null,
                        null,
                        SyntaxFactory.Empty()
                    );

                    nodesAndSeparators.Add(empty);
                }

                if (currentToken.kind == SyntaxKind.CommaToken) {
                    var comma = EatToken();
                    nodesAndSeparators.Add(comma);
                } else {
                    parseNextArgument = false;
                }
            }
        }

        return new SeparatedSyntaxList<ArgumentSyntax>(nodesAndSeparators.ToList());
    }

    private ArgumentSyntax ParseArgument() {
        SyntaxToken name = null;
        SyntaxToken colon = null;

        if (currentToken.kind == SyntaxKind.IdentifierToken && Peek(1).kind == SyntaxKind.ColonToken) {
            name = EatToken();
            colon = Match(SyntaxKind.ColonToken);
        }

        ExpressionSyntax expression;
        if (currentToken.kind is SyntaxKind.CommaToken or SyntaxKind.CloseParenToken)
            expression = SyntaxFactory.Empty();
        else if ((_context & ParserContext.InTemplateArgumentList) != 0 && PeekIsType(0, out _, out _, out _))
            expression = ParseType(false);
        else
            expression = ParseNonAssignmentExpression();

        return SyntaxFactory.Argument(name, colon, expression);
    }

    private SyntaxList<AttributeListSyntax> ParseAttributeLists() {
        var attributeLists = SyntaxListBuilder<AttributeListSyntax>.Create();

        while (currentToken.kind == SyntaxKind.OpenBracketToken)
            attributeLists.Add(ParseAttributeList());

        return attributeLists.ToList();
    }

    private AttributeListSyntax ParseAttributeList() {
        var openBracket = EatToken();

        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextAttribute = true;

        while (parseNextAttribute &&
            currentToken.kind != SyntaxKind.CloseBracketToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            var attribute = ParseAttribute();
            nodesAndSeparators.Add(attribute);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextAttribute = false;
            }
        }

        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.AttributeList(
            openBracket,
            new SeparatedSyntaxList<AttributeSyntax>(nodesAndSeparators.ToList()),
            closeBracket
        );
    }

    private AttributeSyntax ParseAttribute() {
        var identifier = Match(SyntaxKind.IdentifierToken);
        return SyntaxFactory.Attribute(identifier);
    }

    private ExpressionSyntax ParseNullLiteral() {
        var token = Match(SyntaxKind.NullKeyword);
        return SyntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseNumericLiteral() {
        var token = Match(SyntaxKind.NumericLiteralToken);
        return SyntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseBooleanLiteral() {
        var isTrue = currentToken.kind == SyntaxKind.TrueKeyword;
        var keyword = isTrue ? Match(SyntaxKind.TrueKeyword) : Match(SyntaxKind.FalseKeyword);
        return SyntaxFactory.Literal(keyword, isTrue);
    }

    private ExpressionSyntax ParseStringLiteral() {
        var stringToken = Match(SyntaxKind.StringLiteralToken);
        return SyntaxFactory.Literal(stringToken);
    }

    private ArrayRankSpecifierSyntax ParseArrayRankSpecifier(bool allowSize) {
        var openBracket = Match(SyntaxKind.OpenBracketToken);
        ExpressionSyntax size = null;

        if (allowSize && currentToken.kind != SyntaxKind.CloseBracketToken)
            size = ParseExpression();

        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.ArrayRankSpecifier(openBracket, size, closeBracket);
    }

    private SimpleNameSyntax ParseLastCaseName() {
        if (currentToken.kind != SyntaxKind.IdentifierToken) {
            _currentToken = AddDiagnostic(currentToken, Error.ExpectedToken("expression"));
            return SyntaxFactory.IdentifierName(SyntaxFactory.Missing(SyntaxKind.IdentifierToken));
        }

        return ParseSimpleName();
    }

    private SimpleNameSyntax ParseSimpleName() {
        var identifierName = ParseIdentifierName();

        if (identifierName.identifier.isFabricated)
            return identifierName;

        SimpleNameSyntax name = identifierName;

        if (currentToken.kind == SyntaxKind.LessThanToken) {
            var point = GetResetPoint();
            var templateArgumentList = ParseTemplateArgumentList();

            if (templateArgumentList.containsDiagnostics)
                Reset(point);
            else
                name = SyntaxFactory.TemplateName(identifierName.identifier, templateArgumentList);
        }

        return name;
    }

    private IdentifierNameSyntax ParseIdentifierName() {
        var identifier = Match(SyntaxKind.IdentifierToken);
        return SyntaxFactory.IdentifierName(identifier);
    }

    private TypeSyntax ParseType(
        bool allowImplicit = true,
        bool allowRef = true,
        bool hasConstKeyword = false,
        bool allowArraySize = false) {
        if (currentToken.kind == SyntaxKind.RefKeyword) {
            var refKeyword = EatToken();

            if (!allowRef)
                refKeyword = AddDiagnostic(refKeyword, Error.CannotUseRef());

            return SyntaxFactory.ReferenceType(
                refKeyword,
                currentToken.kind == SyntaxKind.ConstKeyword ? EatToken() : null,
                ParseTypeCore(allowImplicit && hasConstKeyword, allowArraySize)
            );
        }

        return ParseTypeCore(allowImplicit && hasConstKeyword, allowArraySize);
    }

    private TypeSyntax ParseTypeCore(bool constAsType, bool allowArraySize) {
        TypeSyntax type;

        if (currentToken.kind is SyntaxKind.ExclamationToken or SyntaxKind.OpenBracketToken ||
            (currentToken.kind == SyntaxKind.IdentifierToken &&
             Peek(1).kind is SyntaxKind.EqualsToken or SyntaxKind.SemicolonToken)) {
            type = SyntaxFactory.EmptyName();
        } else {
            type = ParseUnderlyingType();
        }

        var lastTokenPosition = -1;

        while (IsMakingProgress(ref lastTokenPosition)) {
            switch (currentToken.kind) {
                case SyntaxKind.ExclamationToken:
                    var exclamationToken = EatToken();
                    type = SyntaxFactory.NonNullableType(type, exclamationToken);
                    goto done;
                case SyntaxKind.OpenBracketToken:
                    var rankSpecifiers = SyntaxListBuilder<ArrayRankSpecifierSyntax>.Create();

                    do {
                        rankSpecifiers.Add(ParseArrayRankSpecifier(allowArraySize));
                    } while (currentToken.kind == SyntaxKind.OpenBracketToken);

                    type = SyntaxFactory.ArrayType(type, rankSpecifiers.ToList());
                    continue;
            }
        }

done:
        return type;
    }

    private TypeSyntax ParseUnderlyingType() {
        if (currentToken.kind == SyntaxKind.IdentifierToken)
            return ParseQualifiedName();

        return AddDiagnostic(
            WithFutureDiagnostics(SyntaxFactory.IdentifierName(SyntaxFactory.Missing(SyntaxKind.IdentifierToken))),
            Error.ExpectedToken(SyntaxKind.IdentifierName),
            currentToken.GetLeadingTriviaWidth(),
            currentToken.width
        );
    }

    private NameSyntax ParseQualifiedName() {
        NameSyntax name = ParseSimpleName();

        while (currentToken.kind == SyntaxKind.PeriodToken) {
            var separator = EatToken();
            name = ParseQualifiedNameRight(name, separator);
        }

        return name;
    }

    private NameSyntax ParseQualifiedNameRight(NameSyntax left, SyntaxToken separator) {
        var right = ParseSimpleName();
        return SyntaxFactory.QualifiedName(left, separator, right);
    }
}
