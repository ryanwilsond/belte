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

    private bool _isIncrementalAndFactoryContextMatches {
        get {
            if (!_isIncremental)
                return false;

            var current = currentNode;
            return current is not null;
        }
    }

    /// <summary>
    /// Parses the entirety of a single file.
    /// </summary>
    /// <returns>The parsed file.</returns>
    internal CompilationUnitSyntax ParseCompilationUnit() {
        // TODO How do we distinguish compilation attributes from attributes of the first member?
        // var attributeLists = ParseAttributeLists();
        var usings = ParseUsings();
        var members = ParseMembers(true);
        var endOfFile = Match(SyntaxKind.EndOfFileToken);
        return SyntaxFactory.CompilationUnit(SyntaxFactory.List<AttributeListSyntax>(), usings, members, endOfFile);
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

    private SyntaxList<UsingDirectiveSyntax> ParseUsings() {
        var usings = SyntaxListBuilder<UsingDirectiveSyntax>.Create();

        while (PeekIsUsingDirective()) {
            var usingDirective = ParseUsingDirective();
            usings.Add(usingDirective);
        }

        return usings.ToList();
    }

    private bool PeekIsUsingDirective() {
        if (currentToken.kind == SyntaxKind.UsingKeyword)
            return true;

        if (currentToken.kind == SyntaxKind.GlobalKeyword && Peek(1).kind == SyntaxKind.UsingKeyword)
            return true;

        return false;
    }

    private UsingDirectiveSyntax ParseUsingDirective() {
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.UsingDirective)
            return (UsingDirectiveSyntax)EatNode();

        var globalKeyword = currentToken.kind == SyntaxKind.GlobalKeyword ? EatToken() : null;
        var keyword = Match(SyntaxKind.UsingKeyword);
        var staticKeyword = currentToken.kind == SyntaxKind.StaticKeyword ? EatToken() : null;
        var alias = IsNamedAssignment() ? ParseNameEquals() : null;
        var namespaceOrType = alias is null ? ParseQualifiedName() : ParseType(allowNoFollowUp: true);
        var semicolon = Match(SyntaxKind.SemicolonToken);
        return SyntaxFactory.UsingDirective(globalKeyword, keyword, staticKeyword, alias, namespaceOrType, semicolon);
    }

    private bool IsNamedAssignment() {
        return currentToken.kind == SyntaxKind.IdentifierToken && Peek(1).kind == SyntaxKind.EqualsToken;
    }

    private NameEqualsSyntax ParseNameEquals() {
        var identifier = ParseIdentifierName();
        var equals = EatToken();
        return SyntaxFactory.NameEquals(identifier, equals);
    }

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
        if (_isIncrementalAndFactoryContextMatches &&
            CanReuseMemberDeclaration(_currentNodeKind, allowGlobalStatements)) {
            return (MemberDeclarationSyntax)EatNode();
        }

        var attributeLists = ParseAttributeLists();
        var modifiers = ParseModifiers();

        if ((_context & ParserContext.InClassDefinition) != 0 && currentToken.kind == SyntaxKind.ConstructorKeyword)
            return ParseConstructorDeclaration(attributeLists, modifiers);

        if ((_context & ParserContext.InClassDefinition) != 0 &&
            currentToken.kind is SyntaxKind.ImplicitKeyword or SyntaxKind.ExplicitKeyword) {
            return ParseConversionDeclaration(attributeLists, modifiers);
        }

        switch (currentToken.kind) {
            case SyntaxKind.NamespaceKeyword:
                return ParseNamespaceDeclaration(attributeLists, modifiers);
            case SyntaxKind.StructKeyword:
                return ParseStructDeclaration(attributeLists, modifiers);
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration(attributeLists, modifiers);
            case SyntaxKind.EnumKeyword:
                return ParseEnumDeclaration(attributeLists, modifiers);
        }

        var resetPoint = GetResetPoint();

        var returnType = ParseType();

        if (returnType.kind != SyntaxKind.EmptyName && !returnType.containsDiagnostics) {
            if (currentToken.kind == SyntaxKind.OperatorKeyword)
                return ParseOperatorDeclaration(attributeLists, modifiers, returnType);

            if (PeekIsPostReturnFunction()) {
                return allowGlobalStatements
                    ? SyntaxFactory.GlobalStatement(null, null,
                        ParseLocalFunctionDeclaration(attributeLists, modifiers, returnType))
                    : ParseMethodDeclaration(attributeLists, modifiers, returnType);
            }
        }

        Reset(resetPoint);

        if (allowGlobalStatements)
            return ParseGlobalStatement(attributeLists, modifiers);
        else
            return ParseFieldDeclaration(attributeLists, modifiers);
    }

    private bool PeekIsPostReturnFunction() {
        if (currentToken.kind == SyntaxKind.IdentifierToken) {
            var innerResetPoint = GetResetPoint();
            EatToken();

            if (currentToken.kind == SyntaxKind.OpenParenToken ||
                ScanPossibleTemplateParameterList(out _, out _) == ScanTypeFlags.TemplateTypeOrMethod) {
                Reset(innerResetPoint);
                return true;
            }

            Reset(innerResetPoint);
        }

        return false;
    }

    private bool PeekIsPostTypeLocalDeclaration() {
        if (currentToken.kind == SyntaxKind.IdentifierToken) {
            if (Peek(1).kind is SyntaxKind.EqualsToken or SyntaxKind.SemicolonToken or SyntaxKind.OpenBracketToken)
                return true;
        }

        return false;
    }

    private bool CanReuseMemberDeclaration(SyntaxKind kind, bool isGlobal) {
        switch (kind) {
            case SyntaxKind.ClassDeclaration:
            case SyntaxKind.StructDeclaration:
            case SyntaxKind.EnumDeclaration:
            case SyntaxKind.OperatorDeclaration:
            case SyntaxKind.ConstructorDeclaration:
            case SyntaxKind.NamespaceDeclaration:
            case SyntaxKind.FileScopedNamespaceDeclaration:
                return true;
            case SyntaxKind.FieldDeclaration:
            case SyntaxKind.MethodDeclaration:
                if (!isGlobal)
                    return true;

                return currentNode?.parent is Syntax.CompilationUnitSyntax;
            case SyntaxKind.GlobalStatement:
                return isGlobal;
            default:
                return false;
        }
    }

    private BaseNamespaceDeclarationSyntax ParseNamespaceDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = ParseQualifiedName();

        SyntaxList<UsingDirectiveSyntax> usings;
        SyntaxList<MemberDeclarationSyntax> members;

        if (currentToken.kind == SyntaxKind.SemicolonToken) {
            var semicolon = EatToken();
            usings = ParseUsings();
            members = ParseMembers();

            return SyntaxFactory.FileScopedNamespaceDeclaration(
                attributeLists,
                modifiers,
                keyword,
                identifier,
                semicolon,
                usings,
                members
            );
        }

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        usings = ParseUsings();
        members = ParseMembers();
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.NamespaceDeclaration(
            attributeLists,
            modifiers,
            keyword,
            identifier,
            openBrace,
            usings,
            members,
            closeBrace
        );
    }

    private MemberDeclarationSyntax ParseStructDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var templateParameterList = currentToken.kind == SyntaxKind.LessThanToken
            ? ParseTemplateParameterList()
            : null;
        var constraintClauseList = currentToken.kind == SyntaxKind.WhereKeyword
            ? ParseTemplateConstraintClauseList()
            : null;

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
            constraintClauseList,
            openBrace,
            members,
            closeBrace
        );
    }

    private MemberDeclarationSyntax ParseEnumDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var flagsKeyword = currentToken.kind == SyntaxKind.FlagsKeyword ? EatToken() : null;
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var baseType = currentToken.kind == SyntaxKind.ExtendsKeyword
            ? ParseBaseType()
            : null;

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var members = ParseEnumMembers();
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.EnumDeclaration(
            attributeLists,
            modifiers,
            keyword,
            flagsKeyword,
            identifier,
            null,
            baseType,
            null,
            openBrace,
            null,
            members,
            closeBrace
        );
    }

    private SeparatedSyntaxList<EnumMemberDeclarationSyntax> ParseEnumMembers() {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextMember = true;

        while (parseNextMember &&
            currentToken.kind != SyntaxKind.CloseBraceToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            var expression = ParseEnumMember();
            nodesAndSeparators.Add(expression);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextMember = false;
            }
        }

        return new SeparatedSyntaxList<EnumMemberDeclarationSyntax>(nodesAndSeparators.ToList());
    }

    private EnumMemberDeclarationSyntax ParseEnumMember() {
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.EnumMemberDeclaration)
            return (EnumMemberDeclarationSyntax)EatNode();

        var attributeLists = ParseAttributeLists();
        var modifiers = ParseModifiers();
        var identifier = Match(SyntaxKind.IdentifierToken);
        var equalsValue = currentToken.kind == SyntaxKind.EqualsToken ? ParseEqualsValueClause(false) : null;

        return SyntaxFactory.EnumMemberDeclaration(attributeLists, modifiers, identifier, equalsValue);
    }

    private MemberDeclarationSyntax ParseClassDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var keyword = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenBraceToken);
        var templateParameterList = currentToken.kind == SyntaxKind.LessThanToken
            ? ParseTemplateParameterList()
            : null;
        var baseType = currentToken.kind == SyntaxKind.ExtendsKeyword
            ? ParseBaseType()
            : null;
        var constraintClauseList = currentToken.kind == SyntaxKind.WhereKeyword
            ? ParseTemplateConstraintClauseList()
            : null;

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var saved = _context;
        _context |= ParserContext.InClassDefinition;
        var members = ParseMembers();
        _context = saved;
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.ClassDeclaration(
            attributeLists,
            modifiers,
            keyword,
            identifier,
            templateParameterList,
            baseType,
            constraintClauseList,
            openBrace,
            members,
            closeBrace
        );
    }

    private BaseTypeSyntax ParseBaseType() {
        var extendsKeyword = Match(SyntaxKind.ExtendsKeyword);
        var baseType = ParseSimpleName();
        return SyntaxFactory.BaseType(extendsKeyword, baseType);
    }

    private TemplateConstraintClauseListSyntax ParseTemplateConstraintClauseList() {
        var whereKeyword = Match(SyntaxKind.WhereKeyword);
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var constraintClauses = SyntaxListBuilder<TemplateConstraintClauseSyntax>.Create();

        var lastTokenPosition = -1;

        while (IsMakingProgress(ref lastTokenPosition)) {
            if (currentToken.kind is not SyntaxKind.CloseBraceToken and not SyntaxKind.EndOfFileToken) {
                var constraintClause = ParseTemplateConstraintClause();
                constraintClauses.Add(constraintClause);
            }
        }

        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.TemplateConstraintClauseList(
            whereKeyword,
            openBrace,
            constraintClauses.ToList(),
            closeBrace
        );
    }

    private TemplateConstraintClauseSyntax ParseTemplateConstraintClause() {
        TemplateExtendsConstraintClauseSyntax extendConstraint = null;
        TemplateIsConstraintClauseSyntax isConstraint = null;
        ExpressionStatementSyntax expressionConstraint = null;

        if (Peek(1).kind == SyntaxKind.ExtendsKeyword)
            extendConstraint = ParseTemplateExtendConstraintClause();
        else if (Peek(1).kind == SyntaxKind.IsKeyword)
            isConstraint = ParseTemplateIsConstraintClause();
        else
            expressionConstraint = (ExpressionStatementSyntax)ParseExpressionStatement();

        return SyntaxFactory.TemplateConstraintClause(
            extendConstraint,
            isConstraint,
            expressionConstraint
        );
    }

    private TemplateExtendsConstraintClauseSyntax ParseTemplateExtendConstraintClause() {
        var name = ParseIdentifierName();
        var extendsKeyword = Match(SyntaxKind.ExtendsKeyword);
        var type = ParseSimpleName();
        var semicolon = Match(SyntaxKind.SemicolonToken);
        return SyntaxFactory.TemplateExtendsConstraintClause(name, extendsKeyword, type, semicolon);
    }

    private TemplateIsConstraintClauseSyntax ParseTemplateIsConstraintClause() {
        var name = ParseIdentifierName();
        var isKeyword = Match(SyntaxKind.IsKeyword);
        var keyword = MatchTwo(SyntaxKind.PrimitiveKeyword, SyntaxKind.NotnullKeyword);
        var semicolon = Match(SyntaxKind.SemicolonToken);
        return SyntaxFactory.TemplateIsConstraintClause(name, isKeyword, keyword, semicolon);
    }

    private ConstructorDeclarationSyntax ParseConstructorDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var constructorKeyword = Match(SyntaxKind.ConstructorKeyword, SyntaxKind.OpenParenToken);
        var parameterList = ParseParameterList();
        var constructorInitializer = currentToken.kind == SyntaxKind.ColonToken ? ParseConstructorInitializer() : null;
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.ConstructorDeclaration(
            attributeLists,
            modifiers,
            constructorKeyword,
            parameterList,
            constructorInitializer,
            body
        );
    }

    private ConstructorInitializerSyntax ParseConstructorInitializer() {
        var colon = Match(SyntaxKind.ColonToken);
        var thisOrBaseKeyword = MatchTwo(SyntaxKind.ThisKeyword, SyntaxKind.BaseKeyword);
        var argumentList = ParseArgumentList();

        return SyntaxFactory.ConstructorInitializer(colon, thisOrBaseKeyword, argumentList);
    }

    private MemberDeclarationSyntax ParseMethodDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        TypeSyntax returnType) {
        var identifier = Match(SyntaxKind.IdentifierToken, SyntaxKind.OpenParenToken);
        var templateParameterList = currentToken.kind == SyntaxKind.LessThanToken
            ? ParseTemplateParameterList()
            : null;
        var parameterList = ParseParameterList();
        var constraintClauseList = currentToken.kind == SyntaxKind.WhereKeyword
            ? ParseTemplateConstraintClauseList()
            : null;
        BlockStatementSyntax body = null;
        SyntaxToken semicolon = null;

        if (currentToken.kind == SyntaxKind.SemicolonToken)
            semicolon = Match(SyntaxKind.SemicolonToken);
        else
            body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.MethodDeclaration(
            attributeLists,
            modifiers,
            returnType,
            identifier,
            templateParameterList,
            parameterList,
            constraintClauseList,
            body,
            semicolon
        );
    }

    private MemberDeclarationSyntax ParseOperatorDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        TypeSyntax returnType) {
        var operatorKeyword = Match(SyntaxKind.OperatorKeyword);
        var operatorToken = EatToken();
        var opKind = operatorToken.kind;

        var rightOperatorToken = operatorToken.kind == SyntaxKind.OpenBracketToken
            ? Match(SyntaxKind.CloseBracketToken)
            : null;

        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        switch (parameterList.parameters.Count) {
            case 1:
                if (!operatorToken.isFabricated && !SyntaxFacts.IsOverloadableUnaryOperator(opKind) &&
                    !SyntaxFacts.IsOverloadableMethod(operatorToken)) {
                    operatorToken = AddDiagnostic(
                        operatorToken,
                        Error.ExpectedOverloadableUnaryOperator()
                    );
                }

                break;
            case 2:
                if (!operatorToken.isFabricated && !SyntaxFacts.IsOverloadableBinaryOperator(opKind)) {
                    operatorToken = AddDiagnostic(
                        operatorToken,
                        Error.ExpectedOverloadableBinaryOperator()
                    );
                }

                break;
            default:
                if (operatorToken.isFabricated) {
                    operatorToken = AddDiagnostic(
                        operatorToken,
                        Error.ExpectedOverloadableOperator()
                    );
                } else if (SyntaxFacts.IsOverloadableBinaryOperator(opKind)) {
                    operatorToken = AddDiagnostic(
                        operatorToken,
                        Error.IncorrectBinaryOperatorArgs(SyntaxFacts.GetText(opKind))
                    );
                } else if (SyntaxFacts.IsOverloadableUnaryOperator(opKind) ||
                    SyntaxFacts.IsOverloadableMethod(operatorToken)) {
                    operatorToken = AddDiagnostic(
                        operatorToken,
                        Error.IncorrectUnaryOperatorArgs(SyntaxFacts.GetText(opKind) ?? operatorToken.text)
                    );
                } else {
                    operatorToken = AddDiagnostic(
                        operatorToken,
                        Error.ExpectedOverloadableOperator()
                    );
                }

                break;
        }

        return SyntaxFactory.OperatorDeclaration(
            attributeLists,
            modifiers,
            returnType,
            operatorKeyword,
            operatorToken,
            rightOperatorToken,
            parameterList,
            body
        );
    }

    private ConversionDeclarationSyntax ParseConversionDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers) {
        var implicitOrExplicitKeyword = MatchTwo(SyntaxKind.ImplicitKeyword, SyntaxKind.ExplicitKeyword);
        var operatorKeyword = Match(SyntaxKind.OperatorKeyword);
        var type = ParseType(false);
        var parameterList = ParseParameterList();
        var body = (BlockStatementSyntax)ParseBlockStatement();

        if (parameterList.parameters.Count != 1) {
            operatorKeyword = AddDiagnostic(
                operatorKeyword,
                Error.ExpectedOverloadableUnaryOperator()
            );
        }

        return SyntaxFactory.ConversionDeclaration(
            attributeLists,
            modifiers,
            implicitOrExplicitKeyword,
            operatorKeyword,
            type,
            parameterList,
            body
        );
    }

    private StatementSyntax ParseLocalFunctionDeclaration(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        TypeSyntax returnType) {
        var identifier = Match(SyntaxKind.IdentifierToken);
        var templateParameterList = currentToken.kind == SyntaxKind.LessThanToken
            ? ParseTemplateParameterList()
            : null;
        var parameterList = ParseParameterList();
        var constraintClauseList = currentToken.kind == SyntaxKind.WhereKeyword
            ? ParseTemplateConstraintClauseList()
            : null;
        var body = (BlockStatementSyntax)ParseBlockStatement();

        return SyntaxFactory.LocalFunctionStatement(
            attributeLists,
            modifiers,
            returnType,
            identifier,
            templateParameterList,
            parameterList,
            constraintClauseList,
            body
        );
    }

    private DeclarationModifiers GetModifier(SyntaxToken token) {
        return token.kind switch {
            SyntaxKind.StaticKeyword => DeclarationModifiers.Static,
            SyntaxKind.ConstKeyword => DeclarationModifiers.Const,
            SyntaxKind.ConstexprKeyword => DeclarationModifiers.ConstExpr,
            SyntaxKind.LowlevelKeyword => DeclarationModifiers.LowLevel,
            SyntaxKind.PublicKeyword => DeclarationModifiers.Public,
            SyntaxKind.PrivateKeyword => DeclarationModifiers.Private,
            SyntaxKind.ProtectedKeyword => DeclarationModifiers.Protected,
            SyntaxKind.SealedKeyword => DeclarationModifiers.Sealed,
            SyntaxKind.AbstractKeyword => DeclarationModifiers.Abstract,
            SyntaxKind.VirtualKeyword => DeclarationModifiers.Virtual,
            SyntaxKind.OverrideKeyword => DeclarationModifiers.Override,
            SyntaxKind.NewKeyword => DeclarationModifiers.New,
            SyntaxKind.RefKeyword => DeclarationModifiers.Ref,
            SyntaxKind.ExternKeyword => DeclarationModifiers.Extern,
            SyntaxKind.PinnedKeyword => DeclarationModifiers.Pinned,
            _ => DeclarationModifiers.None,
        };
    }

    private SyntaxList<SyntaxToken> ParseModifiers() {
        var modifiers = SyntaxListBuilder<SyntaxToken>.Create();

        while (true) {
            var modifier = GetModifier(currentToken);

            if (modifier is DeclarationModifiers.None or DeclarationModifiers.Ref or DeclarationModifiers.ConstRef)
                break;

            modifiers.Add(EatToken());
        }

        return modifiers.ToList();
    }

    private SyntaxList<SyntaxToken> ParseParameterModifiers() {
        var modifiers = SyntaxListBuilder<SyntaxToken>.Create();

        while (true) {
            var modifier = GetModifier(currentToken);

            if (modifier is not DeclarationModifiers.Ref and not DeclarationModifiers.Const)
                break;

            modifiers.Add(EatToken());
        }

        return modifiers.ToList();
    }

    private ParameterListSyntax ParseParameterList() {
        if (_isIncrementalAndFactoryContextMatches && CanReuseParameterList(currentNode as Syntax.ParameterListSyntax))
            return (ParameterListSyntax)EatNode();

        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseParameters();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ParameterList(openParenthesis, parameters, closeParenthesis);
    }

    private static bool CanReuseParameterList(Syntax.ParameterListSyntax list) {
        if (list is null)
            return false;

        if (list.openParenthesis.isFabricated)
            return false;

        if (list.closeParenthesis.isFabricated)
            return false;

        foreach (var parameter in list.parameters) {
            if (!CanReuseParameter(parameter))
                return false;
        }

        return true;
    }

    private static bool CanReuseParameter(Syntax.ParameterSyntax parameter) {
        if (parameter is null)
            return false;

        if (parameter.defaultValue is not null)
            return false;

        return true;
    }

    private static bool CanReuseBracketedParameterList(Syntax.TemplateParameterListSyntax list) {
        if (list is null)
            return false;

        if (list.openAngleBracket.isFabricated)
            return false;

        if (list.closeAngleBracket.isFabricated)
            return false;

        foreach (var parameter in list.parameters) {
            if (!CanReuseParameter(parameter))
                return false;
        }

        return true;
    }

    private FunctionPointerParameterListSyntax ParseFunctionPointerParameterList() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var parameters = ParseFunctionPointerParameters();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.FunctionPointerParameterList(openParenthesis, parameters, closeParenthesis);
    }

    private TemplateParameterListSyntax ParseTemplateParameterList() {
        if (_isIncrementalAndFactoryContextMatches &&
            CanReuseBracketedParameterList(currentNode as Syntax.TemplateParameterListSyntax)) {
            return (TemplateParameterListSyntax)EatNode();
        }

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

    private SeparatedSyntaxList<FunctionPointerParameterSyntax> ParseFunctionPointerParameters() {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextParameter = true;
        var saved = _context;
        _context |= ParserContext.InExpression;

        while (parseNextParameter &&
            currentToken.kind != SyntaxKind.CloseParenToken &&
            currentToken.kind != SyntaxKind.EndOfFileToken) {
            var expression = ParseFunctionPointerParameter();
            nodesAndSeparators.Add(expression);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextParameter = false;
            }
        }

        _context = saved;

        return new SeparatedSyntaxList<FunctionPointerParameterSyntax>(nodesAndSeparators.ToList());
    }

    private ParameterSyntax ParseParameter() {
        if (_isIncrementalAndFactoryContextMatches && CanReuseParameter(currentNode as Syntax.ParameterSyntax))
            return (ParameterSyntax)EatNode();

        var attributes = ParseAttributeLists();
        var modifiers = ParseParameterModifiers();
        var type = ParseType(false);
        var identifier = Match(SyntaxKind.IdentifierToken);
        var defaultValue = currentToken.kind == SyntaxKind.EqualsToken
            ? ParseEqualsValueClause(false)
            : null;

        return SyntaxFactory.Parameter(attributes, modifiers, type, identifier, defaultValue);
    }

    private FunctionPointerParameterSyntax ParseFunctionPointerParameter() {
        var attributes = ParseAttributeLists();
        var modifiers = ParseParameterModifiers();
        var type = ParseType(false);

        return SyntaxFactory.FunctionPointerParameter(attributes, modifiers, type);
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
        var declaration = ParseVariableDeclaration();
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

        if (consumedAttributeLists) {
            attributeLists = null;
        } else {
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
        }

        if (consumedModifiers) {
            modifiers = null;
        } else {
            if (modifiers.Any()) {
                var builder = new SyntaxListBuilder<SyntaxToken>(modifiers.Count);

                foreach (var modifier in modifiers) {
                    builder.Add(
                        AddDiagnostic(modifier, Error.InvalidModifier(SyntaxFacts.GetText(modifier.kind)))
                    );
                }

                modifiers = builder.ToList();
            }
        }

        return SyntaxFactory.GlobalStatement(attributeLists, modifiers, statement);
    }

    private VariableDeclarationSyntax ParseVariableDeclaration(TypeSyntax type = null) {
        var inStruct = (_context & ParserContext.InStructDefinition) != 0;
        type ??= ParseType(allowArraySize: true);
        var identifier = Match(SyntaxKind.IdentifierToken);
        BracketedArgumentListSyntax argumentList = null;

        if (currentToken.kind == SyntaxKind.OpenBracketToken)
            argumentList = ParseBracketedArgumentList();

        var initializer = currentToken.kind == SyntaxKind.EqualsToken
            ? ParseEqualsValueClause(inStruct)
            : null;

        return SyntaxFactory.VariableDeclaration(type, identifier, argumentList, initializer);
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
        if (CanReuseStatement(attributeLists)) {
            var reused = (StatementSyntax)EatNode();
            consumedAttributeLists = true;
            consumedModifiers = reused.kind == SyntaxKind.BlockStatement;
            return reused;
        }

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

        bool CanReuseStatement(SyntaxList<AttributeListSyntax> attributes) {
            return _isIncrementalAndFactoryContextMatches &&
                   currentNode is Syntax.StatementSyntax &&
                   attributes?.Count == 0;
        }
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
                consumedModifiers = true;
                return ParseBlockStatement(modifiers);
            case SyntaxKind.SemicolonToken:
                return ParseEmptyStatement();
            case SyntaxKind.IfKeyword:
                return ParseIfOrNullBindingStatement();
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
            case SyntaxKind.SwitchKeyword:
                return ParseSwitchStatement();
            case SyntaxKind.GotoKeyword:
                return ParseGotoStatement();
            case SyntaxKind.ILKeyword:
                return ParseInlineILStatement();
        }

        var resetPoint = GetResetPoint();

        attributeLists ??= ParseAttributeLists();
        modifiers ??= ParseModifiers();
        var type = ParseType(allowArraySize: true);

        if (!type.containsDiagnostics) {
            if (type.kind != SyntaxKind.EmptyName && PeekIsPostReturnFunction()) {
                consumedAttributeLists = true;
                consumedModifiers = true;
                Reset(resetPoint);
                var returnType = ParseType();
                return ParseLocalFunctionDeclaration(attributeLists, modifiers, returnType);
            } else if ((type.kind != SyntaxKind.EmptyName || modifiers?.Any(SyntaxKind.ConstKeyword) == true ||
                modifiers?.Any(SyntaxKind.ConstexprKeyword) == true) && PeekIsPostTypeLocalDeclaration()) {
                consumedAttributeLists = true;
                consumedModifiers = true;
                return ParseLocalDeclarationStatement(attributeLists, modifiers, type);
            }
        }

        Reset(resetPoint);

        return ParseExpressionStatement();
    }

    private StatementSyntax ParseLocalDeclarationStatement(
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<SyntaxToken> modifiers,
        TypeSyntax type) {
        var declaration = ParseVariableDeclaration(type);
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.LocalDeclarationStatement(attributeLists, modifiers, declaration, semicolon);
    }

    private StatementSyntax ParseInlineILStatement() {
        var ilKeyword = EatToken();
        var noVerifyKeyword = currentToken.kind == SyntaxKind.NoVerifyKeyword ? EatToken() : null;
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var instructions = ParseILInstructions();
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.InlineILStatement(ilKeyword, noVerifyKeyword, openBrace, instructions, closeBrace);
    }

    private SyntaxList<ILInstructionSyntax> ParseILInstructions() {
        var instructions = SyntaxListBuilder<ILInstructionSyntax>.Create();
        var startToken = currentToken;

        while (currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            var instruction = ParseILInstruction();
            instructions.Add(instruction);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        }

        return instructions.ToList();
    }

    private ILInstructionSyntax ParseILInstruction() {
        var opCode = Match(SyntaxKind.IdentifierToken);
        SyntaxToken periodOne = null;
        SyntaxToken opCodeSuffixOne = null;
        SyntaxToken periodTwo = null;
        SyntaxToken opCodeSuffixTwo = null;
        SyntaxToken periodThree = null;
        SyntaxToken opCodeSuffixThree = null;

        if (currentToken.kind == SyntaxKind.PeriodToken) {
            periodOne = EatToken();
            opCodeSuffixOne = MatchTwo(SyntaxKind.NumericLiteralToken, SyntaxKind.IdentifierToken);
        }

        if (currentToken.kind == SyntaxKind.PeriodToken) {
            periodTwo = EatToken();
            opCodeSuffixTwo = MatchTwo(SyntaxKind.NumericLiteralToken, SyntaxKind.IdentifierToken);
        }

        if (currentToken.kind == SyntaxKind.PeriodToken) {
            periodThree = EatToken();
            opCodeSuffixThree = MatchTwo(SyntaxKind.NumericLiteralToken, SyntaxKind.IdentifierToken);
        }

        var literal = currentToken.kind is SyntaxKind.NumericLiteralToken or SyntaxKind.StringLiteralToken
            ? EatToken()
            : null;

        TypeSyntax symbol = null;

        if (literal is null) {
            var resetPoint = GetResetPoint();

            var type = ParseType(allowRef: false, allowArraySize: false, allowNoFollowUp: true);

            if (type.kind != SyntaxKind.EmptyName && !type.containsDiagnostics) {
                symbol = type;
            } else {
                Reset(resetPoint);

                if (currentToken.kind is SyntaxKind.GlobalKeyword or SyntaxKind.IdentifierToken)
                    symbol = ParseLastCaseName();
            }
        }

        var parameterList = symbol is not null && currentToken.kind == SyntaxKind.OpenParenToken
            ? ParseFunctionPointerParameterList()
            : null;

        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ILInstruction(
            opCode,
            periodOne,
            opCodeSuffixOne,
            periodTwo,
            opCodeSuffixTwo,
            periodThree,
            opCodeSuffixThree,
            literal,
            symbol,
            parameterList,
            semicolon
        );
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
        var expression = currentToken.kind != SyntaxKind.SemicolonToken ? ParseExpression() : null;
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.ReturnStatement(keyword, expression, semicolon);
    }

    private StatementSyntax ParseGotoStatement() {
        var keyword = EatToken();
        var caseOrDefaultKeyword = MatchTwo(SyntaxKind.CaseKeyword, SyntaxKind.DefaultKeyword);
        var expression = caseOrDefaultKeyword.kind == SyntaxKind.CaseKeyword ? ParseExpression() : null;
        var semicolon = Match(SyntaxKind.SemicolonToken);

        return SyntaxFactory.GotoStatement(keyword, caseOrDefaultKeyword, expression, semicolon);
    }

    private StatementSyntax ParseSwitchStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var expression = ParseExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var sections = ParseSwitchSections();
        var closeBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.SwitchStatement(
            keyword,
            openParenthesis,
            expression,
            closeParenthesis,
            openBrace,
            sections,
            closeBrace
        );
    }

    private SyntaxList<SwitchSectionSyntax> ParseSwitchSections() {
        var statements = SyntaxListBuilder<SwitchSectionSyntax>.Create();
        var startToken = currentToken;

        while (currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            var statement = ParseSwitchSection();
            statements.Add(statement);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        }

        return statements.ToList();
    }

    private SwitchSectionSyntax ParseSwitchSection() {
        var labels = ParseSwitchLabels();
        var statements = ParseStatements(SyntaxKind.CloseBraceToken, SyntaxKind.CaseKeyword, SyntaxKind.DefaultKeyword);
        return SyntaxFactory.SwitchSection(labels, statements);
    }

    private SyntaxList<SwitchLabelSyntax> ParseSwitchLabels() {
        var labels = SyntaxListBuilder<SwitchLabelSyntax>.Create();
        var startToken = currentToken;

        if (currentToken.kind != SyntaxKind.EndOfFileToken) do {
            var label = ParseSwitchLabel();
            labels.Add(label);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        } while (currentToken.kind is SyntaxKind.CaseKeyword or SyntaxKind.DefaultKeyword);

        return labels.ToList();
    }

    private SwitchLabelSyntax ParseSwitchLabel() {
        if (currentToken.kind == SyntaxKind.DefaultKeyword)
            return ParseDefaultSwitchLabel();

        var keyword = Match(SyntaxKind.CaseKeyword);
        var firstExpression = ParseExpression();

        if (currentToken.kind == SyntaxKind.CommaToken)
            return ParseMultiCaseSwitchLabel(keyword, firstExpression);

        var colon = Match(SyntaxKind.ColonToken);
        return SyntaxFactory.CaseSwitchLabel(keyword, firstExpression, colon);
    }

    private SwitchLabelSyntax ParseDefaultSwitchLabel() {
        var keyword = EatToken();
        var colon = Match(SyntaxKind.ColonToken);
        return SyntaxFactory.DefaultSwitchLabel(keyword, colon);
    }

    private SwitchLabelSyntax ParseMultiCaseSwitchLabel(SyntaxToken keyword, ExpressionSyntax firstExpression) {
        var firstComma = EatToken();
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextItem = true;

        nodesAndSeparators.Add(firstExpression);
        nodesAndSeparators.Add(firstComma);

        while (parseNextItem && currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.ColonToken) {
            var expression = ParseExpression();
            nodesAndSeparators.Add(expression);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToList());
        var colon = Match(SyntaxKind.ColonToken);

        return SyntaxFactory.MultiCaseSwitchLabel(keyword, separatedSyntaxList, colon);
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
        if (PeekIsForEachStatement())
            return ParseForEachStatement();

        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);

        var initializer = ParseStatement();
        var condition = currentToken.kind == SyntaxKind.SemicolonToken ? null : ParseNonAssignmentExpression();
        var semicolon = Match(SyntaxKind.SemicolonToken);

        ExpressionSyntax step = null;

        if (currentToken.kind != SyntaxKind.CloseParenToken)
            step = ParseExpression();

        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return SyntaxFactory.ForStatement(
            keyword,
            openParenthesis,
            initializer,
            condition,
            semicolon,
            step,
            closeParenthesis,
            body
        );
    }

    private bool PeekIsForEachStatement() {
        var offset = 0;

        if (Peek(offset++).kind != SyntaxKind.ForKeyword)
            return false;

        if (Peek(offset).kind == SyntaxKind.OpenParenToken)
            offset++;

        if (Peek(offset++).kind != SyntaxKind.IdentifierToken)
            return false;

        if (Peek(offset).kind == SyntaxKind.CommaToken)
            offset++;

        if (Peek(offset).kind == SyntaxKind.IdentifierToken)
            offset++;

        if (Peek(offset).kind != SyntaxKind.InKeyword)
            return false;

        return true;
    }

    private StatementSyntax ParseForEachStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var valueIdentifier = Match(SyntaxKind.IdentifierToken);

        SyntaxToken comma = null;
        SyntaxToken indexIdentifier = null;

        if (currentToken.kind == SyntaxKind.CommaToken) {
            comma = EatToken();
            indexIdentifier = Match(SyntaxKind.IdentifierToken);
        }

        var inKeyword = Match(SyntaxKind.InKeyword);
        var expression = ParseExpression();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var body = ParseStatement();

        return SyntaxFactory.ForEachStatement(
            keyword,
            openParenthesis,
            valueIdentifier,
            comma,
            indexIdentifier,
            inKeyword,
            expression,
            closeParenthesis,
            body
        );
    }

    private StatementSyntax ParseIfOrNullBindingStatement() {
        var keyword = EatToken();
        var openParenthesis = Match(SyntaxKind.OpenParenToken);

        var saved = _context;
        _context |= ParserContext.InIfCondition;

        var condition = ParseExpression();

        _context = saved;

        SyntaxToken minusGreaterThan = null;
        SyntaxToken target = null;
        SyntaxToken exclamation = null;

        if (currentToken.kind == SyntaxKind.MinusGreaterThanToken) {
            minusGreaterThan = EatToken();
            target = Match(SyntaxKind.IdentifierToken);
            exclamation = Match(SyntaxKind.ExclamationToken);
        }

        var closeParenthesis = Match(SyntaxKind.CloseParenToken);
        var then = ParseStatement();

        // Not allow nested if statements with else clause without braces; prevents ambiguous else statements
        // * See BU0023
        var nestedIf = false;
        var inner = then;
        var offset = 0;

        while (inner.kind is SyntaxKind.IfStatement or SyntaxKind.NullBindingStatement) {
            nestedIf = true;
            var innerIf = (BaseIfStatementSyntax)inner;
            offset += innerIf.GetSlotOffset(inner.kind == SyntaxKind.IfStatement ? 4 : 7);

            if (innerIf.elseClause is not null && innerIf.then.kind != SyntaxKind.BlockStatement) {
                var elseOffset = offset + innerIf.then.fullWidth + innerIf.elseClause.GetLeadingTriviaWidth();

                then = AddDiagnostic(
                    then,
                    Error.AmbiguousElse(),
                    elseOffset,
                    innerIf.elseClause.keyword.width
                );
            }

            if (innerIf.then.kind is SyntaxKind.IfStatement or SyntaxKind.NullBindingStatement)
                inner = innerIf.then;
            else
                break;
        }

        var elseClause = ParseElseClause();

        if (elseClause is not null && then.kind != SyntaxKind.BlockStatement && nestedIf) {
            elseClause = AddDiagnostic(
                elseClause,
                Error.AmbiguousElse(),
                elseClause.keyword.GetLeadingTriviaWidth(),
                elseClause.keyword.width
            );
        }

        if (minusGreaterThan is null)
            return SyntaxFactory.IfStatement(keyword, openParenthesis, condition, closeParenthesis, then, elseClause);

        return SyntaxFactory.NullBindingStatement(
            keyword,
            openParenthesis,
            condition,
            minusGreaterThan,
            target,
            exclamation,
            closeParenthesis,
            then,
            elseClause
        );
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

    private StatementSyntax ParseBlockStatement(SyntaxList<SyntaxToken> modifiers = null) {
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.BlockStatement)
            return (BlockStatementSyntax)EatNode();

        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var statements = ParseStatements(SyntaxKind.CloseBraceToken);
        var closeBrace = Match(SyntaxKind.CloseBraceToken);
        return SyntaxFactory.BlockStatement(modifiers, openBrace, statements, closeBrace);
    }

    private SyntaxList<StatementSyntax> ParseStatements(params SyntaxKind[] endDelimiters) {
        var statements = SyntaxListBuilder<StatementSyntax>.Create();
        var startToken = currentToken;

        while (currentToken.kind != SyntaxKind.EndOfFileToken && !endDelimiters.Contains(currentToken.kind)) {
            var statement = ParseStatement();
            statements.Add(statement);

            if (currentToken == startToken)
                EatToken();

            startToken = currentToken;
        }

        return statements.ToList();
    }

    private ExpressionSyntax ParseAssignmentExpression(bool insideCascade = false) {
        var left = ParseOperatorExpression(insideCascade ? SyntaxKind.PeriodPeriodToken.GetPrimaryPrecedence() + 1 : 0);

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
            case SyntaxKind.QuestionExclamationEqualsToken:
            case SyntaxKind.EqualsToken:
                var operatorToken = EatToken();
                var right = ParseAssignmentExpression(insideCascade);
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
        _expectParenthesis = true;
        var value = ParseOperatorExpression();
        _expectParenthesis = false;
        _context = saved;
        return value;
    }

    private ExpressionSyntax ParseExpression(bool allowEmpty = false) {
        var saved = _context;
        _context |= ParserContext.InExpression;
        var expression = ParseAssignmentExpression();
        _context = saved;
        return expression;
    }

    private StatementSyntax ParseEmptyStatement() {
        return SyntaxFactory.EmptyStatement(EatToken());
    }

    private ExpressionSyntax ParseOperatorExpression(int parentPrecedence = 0) {
        ExpressionSyntax left;
        var unaryPrecedence = currentToken.kind.GetUnaryPrecedence();

        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence && !IsTerminator()) {
            var operatorToken = EatToken();

            if (operatorToken.kind is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken) {
                var operand = ParsePrimaryExpression(unaryPrecedence);
                left = SyntaxFactory.PrefixExpression(operatorToken, operand);
            } else {
                var operand = ParseOperatorExpression(unaryPrecedence);
                left = SyntaxFactory.UnaryExpression(operatorToken, operand);
            }
        } else {
            left = ParsePrimaryExpression(parentPrecedence);
        }

        while (true) {
            if (currentToken.kind == SyntaxKind.QuestionToken && !IsStartOfTernary()) {
                var operatorToken = EatToken();
                left = SyntaxFactory.PostfixExpression(left, operatorToken);
                continue;
            }

            break;
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

        bool IsStartOfTernary() {
            var resetPoint = GetResetPoint();

            var opToken = EatToken();
            ParseOperatorExpression(opToken.kind.GetTernaryPrecedence());

            if (currentToken.kind == opToken.kind.GetTernaryOperatorPair()) {
                Reset(resetPoint);
                return true;
            }

            Reset(resetPoint);
            return false;
        }
    }

    private ExpressionSyntax ParsePrimaryExpressionInternal(int parentPrecedence) {
        switch (currentToken.kind) {
            case SyntaxKind.OpenParenToken:
                return ParseCastOrParenthesizedExpression();
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
                return ParseBooleanLiteral();
            case SyntaxKind.NumericLiteralToken:
                return ParseNumericLiteral();
            case SyntaxKind.StringLiteralToken:
                return ParseStringLiteral();
            case SyntaxKind.InterpolatedStringLiteralToken:
                return ParseInterpolatedStringLiteral();
            case SyntaxKind.InterpolatedStringStartToken:
            case SyntaxKind.InterpolatedStringEndToken:
                throw ExceptionUtilities.UnexpectedValue(currentToken.kind);
            case SyntaxKind.CharacterLiteralToken:
                return ParseCharacterLiteral();
            case SyntaxKind.NullKeyword:
                return ParseNullLiteral();
            case SyntaxKind.NullptrKeyword:
                return ParseNullptrLiteral();
            case SyntaxKind.OpenBraceToken:
                return ParseInitializerListOrDictionaryExpression();
            case SyntaxKind.TypeOfKeyword:
                return ParseTypeOfExpression();
            case SyntaxKind.NameOfKeyword:
                return ParseNameOfExpression();
            case SyntaxKind.SizeOfKeyword:
                return ParseSizeOfExpression();
            case SyntaxKind.StackAllocKeyword:
                return ParseStackAllocExpression();
            case SyntaxKind.NewKeyword:
                return ParseObjectOrArrayCreationExpression();
            case SyntaxKind.ThisKeyword:
                return ParseThisExpression();
            case SyntaxKind.BaseKeyword:
                return ParseBaseExpression();
            case SyntaxKind.ThrowKeyword:
                return ParseThrowExpression();
            case SyntaxKind.RefKeyword when parentPrecedence == 0:
                return ParseReferenceExpression();
            case SyntaxKind.RefKeyword when parentPrecedence > 0:
                return AddDiagnostic(ParseReferenceExpression(), Error.InvalidExpressionTerm(SyntaxKind.RefKeyword));
            case SyntaxKind.ColonColonToken:
                return ParseAliasQualifiedName();
            case SyntaxKind.PeriodToken:
                return ParseImplicitEnumFieldExpression();
            case SyntaxKind.IdentifierToken:
            case SyntaxKind.GlobalKeyword:
            default:
                return ParseLastCaseName();
        }
    }

    private ExpressionSyntax ParseImplicitEnumFieldExpression() {
        var period = EatToken();
        var identifier = Match(SyntaxKind.IdentifierToken);
        return SyntaxFactory.ImplicitEnumFieldExpression(period, identifier);
    }

    private ExpressionSyntax ParsePrimaryExpression(int parentPrecedence = 0, ExpressionSyntax left = null) {
        left ??= ParsePrimaryExpressionInternal(parentPrecedence);

        while (true) {
            var startToken = currentToken;
            var precedence = currentToken.kind.GetPrimaryPrecedence();

            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            if (startToken.kind == SyntaxKind.MinusGreaterThanToken && IsNullBindingContractTarget())
                return left;

            left = ParseCorrectPrimaryOperator(left);
            left = ParsePrimaryExpression(precedence, left);

            if (startToken == currentToken)
                EatToken();
        }

        return left;

        ExpressionSyntax ParseCorrectPrimaryOperator(ExpressionSyntax expression) {
            switch (currentToken.kind) {
                case SyntaxKind.OpenParenToken:
                    return ParseCallExpression(expression);
                case SyntaxKind.OpenBracketToken:
                case SyntaxKind.QuestionOpenBracketToken:
                    return ParseIndexExpression(expression);
                case SyntaxKind.PeriodToken:
                case SyntaxKind.QuestionPeriodToken:
                case SyntaxKind.MinusGreaterThanToken when !IsNullBindingContractTarget():
                    return ParseMemberAccessExpression(expression);
                case SyntaxKind.PeriodPeriodToken:
                case SyntaxKind.QuestionPeriodPeriodToken:
                    return ParseCascadeListExpression(expression);
                case SyntaxKind.MinusMinusToken:
                case SyntaxKind.PlusPlusToken:
                case SyntaxKind.ExclamationToken:
                    return ParsePostfixExpression(expression);
                default:
                    return expression;
            }
        }

        bool IsNullBindingContractTarget() {
            if ((_context & ParserContext.InIfCondition) != 0) {
                if (Peek(1).kind == SyntaxKind.IdentifierToken &&
                    Peek(2).kind == SyntaxKind.ExclamationToken &&
                    Peek(3).kind == SyntaxKind.CloseParenToken) {
                    return true;
                }
            }

            return false;
        }
    }

    private ScanTypeFlags ScanType() {
        return ScanType(out _);
    }

    private ScanTypeFlags ScanType(out SyntaxToken lastTokenOfType) {
        return ScanType(ParseTypeMode.Normal, out lastTokenOfType);
    }

    private ScanTypeFlags ScanType(ParseTypeMode mode, out SyntaxToken lastTokenOfType) {
        ScanTypeFlags result;

        if (currentToken.kind == SyntaxKind.RefKeyword) {
            EatToken();

            if (currentToken.kind == SyntaxKind.ConstKeyword)
                EatToken();
        }

        if (currentToken.kind is SyntaxKind.IdentifierToken or SyntaxKind.ColonColonToken) {
            bool isAlias;

            if (currentToken.kind is SyntaxKind.ColonColonToken) {
                result = ScanTypeFlags.NonTemplateTypeOrExpression;
                isAlias = true;
                lastTokenOfType = null;
            } else {
                isAlias = Peek(1).kind == SyntaxKind.ColonColonToken;
                result = ScanNamedTypePart(out lastTokenOfType);

                if (result == ScanTypeFlags.NotType)
                    return ScanTypeFlags.NotType;
            }

            for (var firstLoop = true; currentToken.kind is SyntaxKind.PeriodToken or SyntaxKind.ColonColonToken; firstLoop = false) {
                if (!firstLoop)
                    isAlias = false;

                EatToken();
                result = ScanNamedTypePart(out lastTokenOfType);

                if (result == ScanTypeFlags.NotType)
                    return ScanTypeFlags.NotType;
            }

            if (isAlias)
                result = ScanTypeFlags.AliasQualifiedName;
        } else {
            lastTokenOfType = null;
            return ScanTypeFlags.NotType;
        }

        var lastTokenPosition = -1;

        while (IsMakingProgress(ref lastTokenPosition)) {
            switch (currentToken.kind) {
                case SyntaxKind.ExclamationToken
                        when lastTokenOfType.kind is not SyntaxKind.ExclamationToken
                                                  and not SyntaxKind.AsteriskToken
                                                  and not SyntaxKind.AsteriskAsteriskToken:
                    lastTokenOfType = EatToken();
                    result = ScanTypeFlags.NonNullableType;
                    break;
                case SyntaxKind.AsteriskToken:
                case SyntaxKind.AsteriskAsteriskToken:
                    switch (mode) {
                        default:
                            lastTokenOfType = EatToken();

                            if (result is ScanTypeFlags.TemplateTypeOrExpression or ScanTypeFlags.NonTemplateTypeOrExpression)
                                result = ScanTypeFlags.PointerOrMultiplication;
                            else if (result == ScanTypeFlags.TemplateTypeOrMethod)
                                result = ScanTypeFlags.MustBeType;

                            break;
                    }

                    break;
                case SyntaxKind.OpenParenToken:
                    result = ScanFunctionPointerType(out lastTokenOfType);
                    break;
                case SyntaxKind.OpenBracketToken:
                    EatToken();

                    while (currentToken.kind == SyntaxKind.CommaToken)
                        EatToken();

                    if (currentToken.kind != SyntaxKind.CloseBracketToken) {
                        lastTokenOfType = null;
                        return ScanTypeFlags.NotType;
                    }

                    lastTokenOfType = EatToken();
                    result = ScanTypeFlags.MustBeType;
                    break;
                default:
                    goto done;
            }
        }

done:
        return result;
    }

    private void ScanNamedTypePart() {
        ScanNamedTypePart(out _);
    }

    private ScanTypeFlags ScanNamedTypePart(out SyntaxToken lastTokenOfType) {
        if (currentToken.kind != SyntaxKind.IdentifierToken) {
            lastTokenOfType = null;
            return ScanTypeFlags.NotType;
        }

        lastTokenOfType = EatToken();

        if (currentToken.kind == SyntaxKind.LessThanToken)
            return ScanPossibleTemplateParameterList(out lastTokenOfType, out _);
        else
            return ScanTypeFlags.NonTemplateTypeOrExpression;
    }

    private ScanTypeFlags ScanPossibleTemplateParameterList(
        out SyntaxToken lastTokenOfType,
        out bool isDefinitelyTemplateArgumentList) {
        // TODO Do extra checks after initial fail?
        var resetPoint = GetResetPoint();

        var list = ParseTemplateParameterList();

        Reset(resetPoint);

        if (!list.containsDiagnostics) {
            isDefinitelyTemplateArgumentList = true;
            lastTokenOfType = list.GetLastToken();
            return ScanTypeFlags.TemplateTypeOrMethod;
        }

        lastTokenOfType = null;
        isDefinitelyTemplateArgumentList = false;
        return ScanTypeFlags.NotType;
    }

    private void ScanPossibleTemplateArgumentList(
        int offset,
        out SyntaxToken lastTokenOfType,
        out bool isDefinitelyTemplateArgumentList) {
        var resetPoint = GetResetPoint();

        for (; offset > 0; offset--)
            EatToken();

        var list = ParseTemplateArgumentList();

        Reset(resetPoint);

        if (!list.containsDiagnostics) {
            isDefinitelyTemplateArgumentList = true;
            lastTokenOfType = list.GetLastToken();
            return;
        }

        lastTokenOfType = null;
        isDefinitelyTemplateArgumentList = false;
    }

    private ScanTypeFlags ScanFunctionPointerType(out SyntaxToken lastTokenOfType) {
        var parenthesisStack = 0;
        var parenOffset = 0;

        while (Peek(parenOffset).kind != SyntaxKind.EndOfFileToken) {
            if (Peek(parenOffset).kind == SyntaxKind.OpenParenToken)
                parenthesisStack++;
            else if (Peek(parenOffset).kind == SyntaxKind.CloseParenToken)
                parenthesisStack--;

            if (Peek(parenOffset).kind == SyntaxKind.CloseParenToken && parenthesisStack == 0) {
                parenOffset++;
                break;
            } else {
                parenOffset++;
            }
        }

        if (Peek(parenOffset).kind is not SyntaxKind.AsteriskToken) {
            lastTokenOfType = null;
            return ScanTypeFlags.NotType;
        } else {
            while (parenOffset > 0) {
                EatToken();
                parenOffset--;
            }

            EatToken();

            if (currentToken.kind == SyntaxKind.TildeToken)
                EatToken();

            lastTokenOfType = currentToken;
            return ScanTypeFlags.MustBeType;
        }
    }

    private ExpressionSyntax ParseCastOrParenthesizedExpression() {
        var resetPoint = GetResetPoint();

        if (ScanCast()) {
            Reset(resetPoint);
            return ParseCastExpression();
        }

        Reset(resetPoint);
        return ParseParenthesizedExpression();
    }

    private bool ScanCast() {
        if (currentToken.kind != SyntaxKind.OpenParenToken)
            return false;

        EatToken();

        var type = ScanType();

        if (type == ScanTypeFlags.NotType)
            return false;

        if (currentToken.kind != SyntaxKind.CloseParenToken)
            return false;

        EatToken();

        switch (type) {
            case ScanTypeFlags.PointerOrMultiplication:
            case ScanTypeFlags.NonNullableType:
            case ScanTypeFlags.MustBeType:
            case ScanTypeFlags.AliasQualifiedName:
                return true;
            case ScanTypeFlags.TemplateTypeOrMethod:
                return currentToken.kind == SyntaxKind.OpenBracketToken || CanFollowCast(currentToken.kind);
            case ScanTypeFlags.TemplateTypeOrExpression:
            case ScanTypeFlags.NonTemplateTypeOrExpression:
                if (currentToken.kind == SyntaxKind.OpenBracketToken && Peek(1).kind == SyntaxKind.CloseBracketToken)
                    return true;

                return CanFollowCast(currentToken.kind);
            default:
                throw ExceptionUtilities.UnexpectedValue(type);
        }
    }

    private static bool CanFollowCast(SyntaxKind kind) {
        switch (kind) {
            case SyntaxKind.AsKeyword:
            case SyntaxKind.IsKeyword:
            case SyntaxKind.SemicolonToken:
            case SyntaxKind.CloseParenToken:
            case SyntaxKind.CloseBracketToken:
            case SyntaxKind.OpenBraceToken:
            case SyntaxKind.CloseBraceToken:
            case SyntaxKind.CommaToken:
            case SyntaxKind.EqualsToken:
            case SyntaxKind.PlusEqualsToken:
            case SyntaxKind.MinusEqualsToken:
            case SyntaxKind.AsteriskEqualsToken:
            case SyntaxKind.SlashEqualsToken:
            case SyntaxKind.PercentEqualsToken:
            case SyntaxKind.AmpersandEqualsToken:
            case SyntaxKind.CaretEqualsToken:
            case SyntaxKind.PipeEqualsToken:
            case SyntaxKind.LessThanLessThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
            case SyntaxKind.QuestionToken:
            case SyntaxKind.ColonToken:
            case SyntaxKind.PipePipeToken:
            case SyntaxKind.AmpersandAmpersandToken:
            case SyntaxKind.PipeToken:
            case SyntaxKind.CaretToken:
            case SyntaxKind.EqualsEqualsToken:
            case SyntaxKind.ExclamationEqualsToken:
            case SyntaxKind.LessThanToken:
            case SyntaxKind.LessThanEqualsToken:
            case SyntaxKind.GreaterThanToken:
            case SyntaxKind.GreaterThanEqualsToken:
            case SyntaxKind.QuestionQuestionEqualsToken:
            case SyntaxKind.QuestionExclamationEqualsToken:
            case SyntaxKind.LessThanLessThanToken:
            case SyntaxKind.GreaterThanGreaterThanToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
            case SyntaxKind.SlashToken:
            case SyntaxKind.PercentToken:
            case SyntaxKind.OpenBracketToken:
            case SyntaxKind.PeriodToken:
            case SyntaxKind.MinusGreaterThanToken:
            case SyntaxKind.QuestionQuestionToken:
            case SyntaxKind.QuestionExclamationToken:
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.AsteriskToken:
            case SyntaxKind.PlusPlusToken:
            case SyntaxKind.MinusMinusToken:
            case SyntaxKind.AmpersandToken:
            case SyntaxKind.EndOfFileToken:
                return false;
            default:
                return true;
        }
    }

    private ExpressionSyntax ParseCastExpression() {
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false, false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        // ? We treat casts as unary precedence so we grab a random unary operator
        // ? We can't set ParenToken as an actual unary operator because then ParseUnaryExpression grabs parens before
        // ? we can parse parenthesized expressions properly
        var expression = ParseOperatorExpression(SyntaxKind.DollarToken.GetUnaryPrecedence());

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

    private ExpressionSyntax ParseInitializerListOrDictionaryExpression() {
        var left = Match(SyntaxKind.OpenBraceToken);

        if (currentToken.kind == SyntaxKind.CloseBraceToken)
            return ParseInitializerListExpression(left);

        var point = GetResetPoint();
        var initializerDictionary = ParseInitializerDictionaryExpression(left);

        if (!initializerDictionary.containsDiagnostics)
            return initializerDictionary;

        Reset(point);
        return ParseInitializerListExpression(left);
    }

    private ExpressionSyntax ParseInitializerListExpression(SyntaxToken leftBrace) {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextItem = true;

        while (parseNextItem && currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            var expression = ParseExpression();
            nodesAndSeparators.Add(expression);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToList());
        var rightBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.InitializerListExpression(leftBrace, separatedSyntaxList, rightBrace);
    }

    private ExpressionSyntax ParseInitializerDictionaryExpression(SyntaxToken leftBrace) {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextItem = true;

        while (parseNextItem && currentToken.kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.CloseBraceToken) {
            var keyValuePair = ParseKeyValuePair();
            nodesAndSeparators.Add(keyValuePair);

            if (currentToken.kind == SyntaxKind.CommaToken) {
                var comma = EatToken();
                nodesAndSeparators.Add(comma);
            } else {
                parseNextItem = false;
            }
        }

        var separatedSyntaxList = new SeparatedSyntaxList<KeyValuePairSyntax>(nodesAndSeparators.ToList());
        var rightBrace = Match(SyntaxKind.CloseBraceToken);

        return SyntaxFactory.InitializerDictionaryExpression(leftBrace, separatedSyntaxList, rightBrace);
    }

    private KeyValuePairSyntax ParseKeyValuePair() {
        var key = ParseExpression();
        var colon = Match(SyntaxKind.ColonToken);
        var value = ParseExpression();
        return SyntaxFactory.KeyValuePair(key, colon, value);
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

    private ExpressionSyntax ParseNameOfExpression() {
        var keyword = Match(SyntaxKind.NameOfKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var name = ParseQualifiedName();
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.NameOfExpression(keyword, openParenthesis, name, closeParenthesis);
    }

    private ExpressionSyntax ParseSizeOfExpression() {
        var keyword = Match(SyntaxKind.SizeOfKeyword);
        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var type = ParseType(false);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.SizeOfExpression(keyword, openParenthesis, type, closeParenthesis);
    }

    private ExpressionSyntax ParseStackAllocExpression() {
        var keyword = Match(SyntaxKind.StackAllocKeyword);
        var type = ParseType(false, allowArraySize: true);
        return SyntaxFactory.StackAllocExpression(keyword, type);
    }

    private ExpressionSyntax ParseObjectOrArrayCreationExpression() {
        var keyword = Match(SyntaxKind.NewKeyword);
        var type = ParseType(allowArraySize: true);

        if (IsArrayType(type))
            return ParseArrayCreationExpression(keyword, type);
        else
            return ParseObjectCreationExpression(keyword, type);

        static bool IsArrayType(TypeSyntax syntax) {
            if (syntax is ArrayTypeSyntax a)
                return true;
            else if (syntax is NonNullableTypeSyntax n)
                return IsArrayType(n.type);
            else if (syntax is ReferenceTypeSyntax r)
                return IsArrayType(r.type);
            else
                return false;
        }
    }

    private ExpressionSyntax ParseObjectCreationExpression(SyntaxToken newKeyword, TypeSyntax type) {
        var argumentList = ParseArgumentList();
        return SyntaxFactory.ObjectCreationExpression(newKeyword, type, argumentList);
    }

    private ExpressionSyntax ParseArrayCreationExpression(SyntaxToken newKeyword, TypeSyntax type) {
        var initializer = currentToken.kind == SyntaxKind.OpenBraceToken
            ? (InitializerListExpressionSyntax)ParseInitializerListExpression(EatToken())
            : null;

        return SyntaxFactory.ArrayCreationExpression(newKeyword, type, initializer);
    }

    private ExpressionSyntax ParseThisExpression() {
        var keyword = Match(SyntaxKind.ThisKeyword);
        return SyntaxFactory.ThisExpression(keyword);
    }

    private ExpressionSyntax ParseBaseExpression() {
        var keyword = Match(SyntaxKind.BaseKeyword);
        return SyntaxFactory.BaseExpression(keyword);
    }

    private ExpressionSyntax ParseThrowExpression() {
        var keyword = Match(SyntaxKind.ThrowKeyword);
        var expression = ParseExpression();
        return SyntaxFactory.ThrowExpression(keyword, expression);
    }

    private ExpressionSyntax ParseMemberAccessExpression(ExpressionSyntax expression) {
        var operatorToken = EatToken();
        var name = ParseSimpleName();

        return SyntaxFactory.MemberAccessExpression(expression, operatorToken, name);
    }

    private ExpressionSyntax ParseCascadeListExpression(ExpressionSyntax expression) {
        var cascades = ParseCascadeList();
        return SyntaxFactory.CascadeListExpression(expression, cascades);
    }

    private SyntaxList<CascadeExpressionSyntax> ParseCascadeList() {
        var nodes = SyntaxListBuilder<CascadeExpressionSyntax>.Create();
        var parseNextCascade = true;

        while (parseNextCascade && currentToken.kind != SyntaxKind.EndOfFileToken) {
            if (currentToken.kind is SyntaxKind.PeriodPeriodToken or SyntaxKind.QuestionPeriodPeriodToken) {
                var cascade = ParseCascadeExpression();
                nodes.Add(cascade);
            } else {
                parseNextCascade = false;
            }
        }

        return nodes.ToList();
    }

    private CascadeExpressionSyntax ParseCascadeExpression() {
        var op = MatchTwo(SyntaxKind.PeriodPeriodToken, SyntaxKind.QuestionPeriodPeriodToken);
        var expression = ParseNonCascadeListExpression();
        return SyntaxFactory.CascadeExpression(op, expression);
    }

    private ExpressionSyntax ParseNonCascadeListExpression() {
        return ParseAssignmentExpression(true);
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax expression) {
        var argumentList = ParseBracketedArgumentList();
        return SyntaxFactory.IndexExpression(expression, argumentList);
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
            SyntaxKind.OpenParenToken or SyntaxKind.EndOfFileToken
                => ScanTemplateArgumentListKind.PossibleTemplateArgumentList,
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

    private BracketedArgumentListSyntax ParseBracketedArgumentList() {
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.BracketedArgumentList)
            return (BracketedArgumentListSyntax)EatNode();

        var openBracket = MatchTwo(SyntaxKind.OpenBracketToken, SyntaxKind.QuestionOpenBracketToken);
        _bracketStack.Push(SyntaxKind.CloseBracketToken);
        var arguments = ParseArguments(SyntaxKind.CloseBracketToken);
        _bracketStack.Pop();
        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.BracketedArgumentList(openBracket, arguments, closeBracket);
    }

    private ArgumentListSyntax ParseArgumentList() {
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.ArgumentList)
            return (ArgumentListSyntax)EatNode();

        var openParenthesis = Match(SyntaxKind.OpenParenToken);
        var arguments = ParseArguments(SyntaxKind.CloseParenToken);
        var closeParenthesis = Match(SyntaxKind.CloseParenToken);

        return SyntaxFactory.ArgumentList(openParenthesis, arguments, closeParenthesis);
    }

    private SeparatedSyntaxList<BaseArgumentSyntax> ParseArguments(SyntaxKind closeBracket) {
        var nodesAndSeparators = SyntaxListBuilder<BelteSyntaxNode>.Create();
        var parseNextArgument = true;

        if (currentToken.kind != SyntaxKind.CloseParenToken) {
            while (parseNextArgument && currentToken.kind != SyntaxKind.EndOfFileToken) {
                if (currentToken.kind != SyntaxKind.CommaToken && currentToken.kind != closeBracket) {
                    var argument = ParseArgument();
                    nodesAndSeparators.Add(argument);
                } else {
                    nodesAndSeparators.Add(
                        SyntaxFactory.OmittedArgument(SyntaxFactory.Token(SyntaxKind.OmittedArgumentToken))
                    );
                }

                if (currentToken.kind == SyntaxKind.CommaToken) {
                    var comma = EatToken();
                    nodesAndSeparators.Add(comma);
                } else {
                    parseNextArgument = false;
                }
            }
        }

        return new SeparatedSyntaxList<BaseArgumentSyntax>(nodesAndSeparators.ToList());
    }

    private ArgumentSyntax ParseArgument() {
        SyntaxToken name = null;
        SyntaxToken colon = null;
        SyntaxToken refKeyword = null;

        if (currentToken.kind == SyntaxKind.IdentifierToken && Peek(1).kind == SyntaxKind.ColonToken) {
            name = EatToken();
            colon = EatToken();
        }

        if (currentToken.kind == SyntaxKind.RefKeyword)
            refKeyword = EatToken();

        ExpressionSyntax expression;

        if ((_context & ParserContext.InTemplateArgumentList) != 0) {
            var resetPoint = GetResetPoint();
            var type = ParseType(allowRef: false);

            if (type.kind != SyntaxKind.EmptyName && !type.containsDiagnostics) {
                expression = type;
            } else {
                Reset(resetPoint);
                expression = ParseNonAssignmentExpression();
            }
        } else {
            expression = ParseNonAssignmentExpression();
        }

        return SyntaxFactory.Argument(name, colon, refKeyword, expression);
    }

    private SyntaxList<AttributeListSyntax> ParseAttributeLists() {
        var attributeLists = SyntaxListBuilder<AttributeListSyntax>.Create();

        while (currentToken.kind == SyntaxKind.OpenBracketToken)
            attributeLists.Add(ParseAttributeList());

        return attributeLists.ToList();
    }

    private AttributeListSyntax ParseAttributeList() {
        if (_isIncrementalAndFactoryContextMatches &&
            _currentNodeKind == SyntaxKind.AttributeList &&
            (_context & ParserContext.InExpression) == 0) {
            return (AttributeListSyntax)EatNode();
        }

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
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.Attribute)
            return (AttributeSyntax)EatNode();

        var name = ParseQualifiedName();
        var arguments = currentToken.kind == SyntaxKind.OpenParenToken ? ParseArgumentList() : null;
        return SyntaxFactory.Attribute(name, arguments);
    }

    private ExpressionSyntax ParseNullLiteral() {
        var token = Match(SyntaxKind.NullKeyword);
        return SyntaxFactory.Literal(token);
    }

    private ExpressionSyntax ParseNullptrLiteral() {
        var token = Match(SyntaxKind.NullptrKeyword);
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

    private ExpressionSyntax ParseInterpolatedStringLiteral() {
        var originalToken = EatToken();
        var originalText = originalToken.text;
        var interpolations = SyntaxListBuilder<InterpolatedStringContentSyntax>.Create();

        var tempLexer = new Lexer(SourceText.From(originalText), options, allowPreprocessorDirectives: false);
        var groups = tempLexer.RereadInterpolatedString(out var hasCloseQuote);

        foreach (var group in groups) {
            if (group.Length == 1 && group[0].kind == SyntaxKind.StringLiteralToken)
                interpolations.Add(SyntaxFactory.InterpolatedStringText(group[0]));
            else
                interpolations.Add(ParseInterpolation(group));
        }

        var leading = originalToken.GetLeadingTrivia();
        var openQuote = SyntaxFactory.Token(
            SyntaxKind.InterpolatedStringStartToken,
            2 + (leading?.fullWidth ?? 0),
            originalText[0..2],
            null,
            leading,
            null
        );

        var trailing = originalToken.GetTrailingTrivia();
        var closeQuote = hasCloseQuote
            ? SyntaxFactory.Token(
                SyntaxKind.InterpolatedStringEndToken,
                1 + (trailing?.fullWidth ?? 0),
                originalText[^1].ToString(),
                null,
                null,
                trailing)
            : SyntaxFactory.Missing(
                SyntaxKind.InterpolatedStringEndToken,
                null,
                trailing);

        return SyntaxFactory.InterpolatedStringExpression(openQuote, interpolations.ToList(), closeQuote);
    }

    private InterpolationSyntax ParseInterpolation(SyntaxToken[] tokens) {
        var openBrace = tokens[0];
        ExpressionSyntax expression = null;
        SyntaxToken closeBrace;

        if (tokens.Length == 2 && tokens[1].kind == SyntaxKind.CloseBraceToken) {
            closeBrace = tokens[1];
        } else {
            if (tokens.Length > 1) {
                var tempLexer = new Lexer(SourceText.From(tokens[1].text), options, allowPreprocessorDirectives: false);
                var tempParser = new LanguageParser(tempLexer, oldTree: null, changes: null);

                expression = tempParser.ParseExpression(true);
                var report = true;

                while (tempParser.currentToken.kind != SyntaxKind.EndOfFileToken) {
                    var unexpected = tempParser.EatToken(stallDiagnostics: true);

                    if (report) {
                        report = false;
                        expression = tempParser.AddDiagnostic(
                            tempParser.WithFutureDiagnostics(tempParser.AddTrailingSkippedSyntax(expression, unexpected)),
                            Error.UnexpectedToken(unexpected.kind),
                            unexpected.GetLeadingTriviaWidth(),
                            unexpected.width
                        );
                    } else {
                        expression = tempParser.WithFutureDiagnostics(
                            tempParser.AddTrailingSkippedSyntax(expression, unexpected)
                        );
                    }
                }
            }

            if (tokens.Length == 3) {
                closeBrace = tokens[2];
            } else {
                var unexpectedToken = EatToken();

                closeBrace = AddDiagnostic(
                    AddLeadingSkippedSyntax(SyntaxFactory.Missing(SyntaxKind.CloseBraceToken), unexpectedToken),
                    GetUnexpectedTokenError(unexpectedToken.kind, SyntaxKind.CloseBraceToken),
                    unexpectedToken.GetLeadingTriviaWidth(),
                    unexpectedToken.width
                );
            }
        }

        return SyntaxFactory.Interpolation(openBrace, expression, closeBrace);
    }

    private ExpressionSyntax ParseCharacterLiteral() {
        var characterToken = Match(SyntaxKind.CharacterLiteralToken);
        return SyntaxFactory.Literal(characterToken);
    }

    private ArrayRankSpecifierSyntax ParseArrayRankSpecifier(bool allowSize) {
        var openBracket = Match(SyntaxKind.OpenBracketToken);
        var size = (allowSize && currentToken.kind != SyntaxKind.CloseBracketToken)
            ? ParseExpression()
            : null;

        var closeBracket = Match(SyntaxKind.CloseBracketToken);

        return SyntaxFactory.ArrayRankSpecifier(openBracket, size, closeBracket);
    }

    private NameSyntax ParseLastCaseName() {
        if (currentToken.kind is not SyntaxKind.IdentifierToken and not SyntaxKind.GlobalKeyword) {
            _currentToken = AddDiagnostic(currentToken, Error.ExpectedToken("expression"));
            return SyntaxFactory.IdentifierName(SyntaxFactory.Missing(SyntaxKind.IdentifierToken));
        }

        return ParseAliasQualifiedName();
    }

    private SimpleNameSyntax ParseSimpleName(bool allowGlobal = false) {
        var identifierName = ParseIdentifierName(allowGlobal);

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

    private IdentifierNameSyntax ParseIdentifierName(bool allowGlobal = false) {
        if (_isIncrementalAndFactoryContextMatches && _currentNodeKind == SyntaxKind.IdentifierName)
            return (IdentifierNameSyntax)EatNode();

        var identifier = (allowGlobal && currentToken.kind == SyntaxKind.GlobalKeyword)
            ? EatToken()
            : Match(SyntaxKind.IdentifierToken);

        return SyntaxFactory.IdentifierName(identifier);
    }

    private TypeSyntax ParseType(bool allowRef = true, bool allowArraySize = false, bool allowNoFollowUp = false) {
        if (currentToken.kind == SyntaxKind.RefKeyword) {
            if (allowRef) {
                var refKeyword = EatToken();

                return SyntaxFactory.ReferenceType(
                    refKeyword,
                    currentToken.kind == SyntaxKind.ConstKeyword ? EatToken() : null,
                    ParseTypeCore(allowArraySize, allowNoFollowUp)
                );
            } else {
                var unexpected = EatToken(stallDiagnostics: true);

                return AddDiagnostic(
                    WithFutureDiagnostics(
                        AddLeadingSkippedSyntax(ParseTypeCore(allowArraySize, allowNoFollowUp), unexpected)
                    ),
                    Error.CannotUseRef(),
                    unexpected.GetLeadingTriviaWidth(),
                    unexpected.width
                );
            }
        }

        return ParseTypeCore(allowArraySize, allowNoFollowUp);
    }

    private TypeSyntax ParseTypeCore(bool allowArraySize, bool allowNoFollowUp) {
        TypeSyntax type;

        if (currentToken.kind is SyntaxKind.ExclamationToken or SyntaxKind.OpenBracketToken ||
            (!allowNoFollowUp && currentToken.kind == SyntaxKind.IdentifierToken &&
             Peek(1).kind is SyntaxKind.EqualsToken or SyntaxKind.SemicolonToken)) {
            type = SyntaxFactory.EmptyName();
        } else {
            type = ParseUnderlyingType();
        }

        var lastTokenPosition = -1;

        while (IsMakingProgress(ref lastTokenPosition)) {
            switch (currentToken.kind) {
                case SyntaxKind.ExclamationToken when CanBeNonNullable():
                    var exclamationToken = EatToken();
                    type = SyntaxFactory.NonNullableType(type, exclamationToken);
                    continue;

                    bool CanBeNonNullable() {
                        if (type.kind == SyntaxKind.NonNullableType)
                            return false;

                        return true;
                    }
                case SyntaxKind.OpenBracketToken:
                    var rankSpecifiers = SyntaxListBuilder<ArrayRankSpecifierSyntax>.Create();

                    do {
                        rankSpecifiers.Add(ParseArrayRankSpecifier(allowArraySize));
                    } while (currentToken.kind == SyntaxKind.OpenBracketToken);

                    type = SyntaxFactory.ArrayType(type, rankSpecifiers.ToList());
                    continue;
                case SyntaxKind.AsteriskToken:
                    var asteriskToken = EatToken();
                    type = SyntaxFactory.PointerType(type, asteriskToken);
                    continue;
                case SyntaxKind.AsteriskAsteriskToken:
                    var asteriskAsterisk = EatToken();
                    type = SyntaxFactory.PointerType(type, asteriskAsterisk);
                    type = SyntaxFactory.PointerType(type, SyntaxFactory.Token(SyntaxKind.None));
                    continue;
                case SyntaxKind.OpenParenToken: {
                        var parenthesisStack = 0;
                        var parenOffset = 0;

                        while (Peek(parenOffset).kind != SyntaxKind.EndOfFileToken) {
                            if (Peek(parenOffset).kind == SyntaxKind.OpenParenToken)
                                parenthesisStack++;
                            else if (Peek(parenOffset).kind == SyntaxKind.CloseParenToken)
                                parenthesisStack--;

                            if (Peek(parenOffset).kind == SyntaxKind.CloseParenToken && parenthesisStack == 0) {
                                parenOffset++;
                                break;
                            } else {
                                parenOffset++;
                            }
                        }

                        if (Peek(parenOffset).kind is not SyntaxKind.AsteriskToken)
                            continue;

                        var paramList = ParseFunctionPointerParameterList();
                        var asterisk = Match(SyntaxKind.AsteriskToken);
                        SyntaxToken callingConvention = null;

                        if (currentToken.kind == SyntaxKind.TildeToken)
                            callingConvention = EatToken();

                        type = SyntaxFactory.FunctionPointer(type, paramList, asterisk, callingConvention);
                    }

                    continue;
            }
        }

        return type;
    }

    private TypeSyntax ParseUnderlyingType() {
        if (currentToken.kind is SyntaxKind.IdentifierToken or SyntaxKind.GlobalKeyword)
            return ParseQualifiedName();

        return AddDiagnostic(
            WithFutureDiagnostics(SyntaxFactory.IdentifierName(SyntaxFactory.Missing(SyntaxKind.IdentifierToken))),
            Error.ExpectedToken(SyntaxKind.IdentifierName),
            currentToken.GetLeadingTriviaWidth(),
            currentToken.width
        );
    }

    private NameSyntax ParseAliasQualifiedName() {
        var name = ParseSimpleName(true);
        return currentToken.kind == SyntaxKind.ColonColonToken
            ? ParseQualifiedNameRight(name, EatToken())
            : name;
    }

    private NameSyntax ParseQualifiedName() {
        var name = ParseAliasQualifiedName();

        while (currentToken.kind is SyntaxKind.PeriodToken or SyntaxKind.ColonColonToken) {
            var separator = EatToken();
            name = ParseQualifiedNameRight(name, separator);
        }

        return name;
    }

    private NameSyntax ParseQualifiedNameRight(NameSyntax left, SyntaxToken separator) {
        var right = ParseSimpleName();

        switch (separator.kind) {
            case SyntaxKind.PeriodToken:
                return SyntaxFactory.QualifiedName(left, separator, right);
            case SyntaxKind.ColonColonToken:
                if (left.kind != SyntaxKind.IdentifierName)
                    separator = AddDiagnostic(separator, Error.UnexpectedAliasName());

                if (left is not IdentifierNameSyntax identifier) {
                    separator = ConvertToMissingWithTrailingTrivia(separator, SyntaxKind.PeriodToken);
                    return SyntaxFactory.QualifiedName(left, separator, right);
                } else {
                    return WithAdditionalDiagnostics(
                        SyntaxFactory.AliasQualifiedName(identifier, separator, right),
                        left.GetDiagnostics()
                    );
                }
            default:
                throw ExceptionUtilities.Unreachable();
        }
    }
}
