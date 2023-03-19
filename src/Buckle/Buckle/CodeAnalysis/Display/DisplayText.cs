using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Represents a piece of text with classifications for segments of text.
/// Can be multiple lines.
/// </summary>
internal sealed class DisplayText {
    private List<DisplayTextSegment> _segments;
    private bool _writeIndent = true;

    internal DisplayText() {
        _segments = new List<DisplayTextSegment>();
        indent = 0;
    }

    /// <summary>
    /// Determines the indentation to use at any given moment.
    /// Usually represented as tabs.
    /// </summary>
    internal int indent { get; set; }

    /// <summary>
    /// Converts the <see cref="DisplayText" /> into a raw string, scrapping all the Classifications.
    /// </summary>
    public override string ToString() {
        var builder = new StringBuilder();

        foreach (var segment in _segments)
            builder.Append(segment.text);

        return builder.ToString();
    }

    internal ImmutableArray<DisplayTextSegment> Flush() {
        var array = ImmutableArray.CreateRange<DisplayTextSegment>(_segments);
        _segments.Clear();

        return array;
    }

    /// <summary>
    /// Appends a <see cref="DisplayTextSegment" /> to the end of this <see cref="DisplayText" />.
    /// </summary>
    /// <param name="segment"><see cref="DisplayTextSegment" /> to append.</param>
    internal void Write(DisplayTextSegment segment) {
        if (_writeIndent) {
            _writeIndent = false;

            for (int i=0; i<indent; i++)
                _segments.Add(DisplayTextSegment.CreateIndent());
        }

        _segments.Add(segment);

        if (segment.classification == Classification.Line)
            _writeIndent = true;
    }

    /// <summary>
    /// Generates a <see cref="DisplayText" /> off of a single <see cref="BoundNode" />'s tree.
    /// </summary>
    /// <param name="node"><see cref="BoundNode" /> to generate a text from.</param>
    /// <return>Generated text.</return>
    internal static DisplayText DisplayNode(BoundNode node) {
        var text = new DisplayText();
        DisplayNode(text, node);

        return text;
    }

    /// <summary>
    /// Appends a <see cref="BoundNode" /> to the end of an existing <see cref="DisplayText" />.
    /// </summary>
    /// <param name="text">Existing text.</param>
    /// <param name="node"><see cref="BoundNode" /> to append.</param>
    internal static void DisplayNode(DisplayText text, BoundNode node) {
        switch (node.kind) {
            case BoundNodeKind.Type:
                DisplayType(text, (BoundType)node);
                break;
            case BoundNodeKind.NopStatement:
                DisplayNopStatement(text, (BoundNopStatement)node);
                break;
            case BoundNodeKind.BlockStatement:
                DisplayBlockStatement(text, (BoundBlockStatement)node);
                break;
            case BoundNodeKind.ExpressionStatement:
                DisplayExpressionStatement(text, (BoundExpressionStatement)node);
                break;
            case BoundNodeKind.VariableDeclarationStatement:
                DisplayVariableDeclarationStatement(text, (BoundVariableDeclarationStatement)node);
                break;
            case BoundNodeKind.IfStatement:
                DisplayIfStatement(text, (BoundIfStatement)node);
                break;
            case BoundNodeKind.WhileStatement:
                DisplayWhileStatement(text, (BoundWhileStatement)node);
                break;
            case BoundNodeKind.ForStatement:
                DisplayForStatement(text, (BoundForStatement)node);
                break;
            case BoundNodeKind.GotoStatement:
                DisplayGotoStatement(text, (BoundGotoStatement)node);
                break;
            case BoundNodeKind.LabelStatement:
                DisplayLabelStatement(text, (BoundLabelStatement)node);
                break;
            case BoundNodeKind.ConditionalGotoStatement:
                DisplayConditionalGotoStatement(text, (BoundConditionalGotoStatement)node);
                break;
            case BoundNodeKind.DoWhileStatement:
                DisplayDoWhileStatement(text, (BoundDoWhileStatement)node);
                break;
            case BoundNodeKind.ReturnStatement:
                DisplayReturnStatement(text, (BoundReturnStatement)node);
                break;
            case BoundNodeKind.TryStatement:
                DisplayTryStatement(text, (BoundTryStatement)node);
                break;
            case BoundNodeKind.TernaryExpression:
                DisplayTernaryExpression(text, (BoundTernaryExpression)node);
                break;
            case BoundNodeKind.IndexExpression:
                DisplayIndexExpression(text, (BoundIndexExpression)node);
                break;
            case BoundNodeKind.ReferenceExpression:
                DisplayReferenceExpression(text, (BoundReferenceExpression)node);
                break;
            case BoundNodeKind.UnaryExpression:
                DisplayUnaryExpression(text, (BoundUnaryExpression)node);
                break;
            case BoundNodeKind.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    DisplayInitializerListExpression(text, il);
                else
                    DisplayLiteralExpression(text, (BoundLiteralExpression)node);

                break;
            case BoundNodeKind.BinaryExpression:
                DisplayBinaryExpression(text, (BoundBinaryExpression)node);
                break;
            case BoundNodeKind.VariableExpression:
                DisplayVariableExpression(text, (BoundVariableExpression)node);
                break;
            case BoundNodeKind.AssignmentExpression:
                DisplayAssignmentExpression(text, (BoundAssignmentExpression)node);
                break;
            case BoundNodeKind.EmptyExpression:
                DisplayEmptyExpression(text, (BoundEmptyExpression)node);
                break;
            case BoundNodeKind.ErrorExpression:
                DisplayErrorExpression(text, (BoundErrorExpression)node);
                break;
            case BoundNodeKind.CallExpression:
                DisplayCallExpression(text, (BoundCallExpression)node);
                break;
            case BoundNodeKind.CastExpression:
                DisplayCastExpression(text, (BoundCastExpression)node);
                break;
            case BoundNodeKind.TypeOfExpression:
                DisplayTypeOfExpression(text, (BoundTypeOfExpression)node);
                break;
            case BoundNodeKind.ConstructorExpression:
                DisplayConstructorExpression(text, (BoundConstructorExpression)node);
                break;
            case BoundNodeKind.MemberAccessExpression:
                DisplayMemberAccessExpression(text, (BoundMemberAccessExpression)node);
                break;
            case BoundNodeKind.TypeWrapper:
                DisplayLiteralExpression(
                    text, new BoundLiteralExpression(((BoundTypeWrapper)node).constantValue.value)
                );
                break;
            default:
                throw new BelteInternalException($"WriteTo: unexpected node '{node.kind}'");
        }
    }

    private static void DisplayType(DisplayText text, BoundType type) {
        text.Write(CreateType(type.BaseType().ToString()));
        var brackets = "";

        for (int i=0; i<type.dimensions; i++)
            brackets += "[]";

        text.Write(CreatePunctuation(brackets));
    }

    private static void DisplayNopStatement(DisplayText text, BoundNopStatement node) {
        text.Write(CreateKeyword("nop"));
        text.Write(CreateLine());
    }

    private static void DisplayBlockStatement(DisplayText text, BoundBlockStatement node, bool newLine = true) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.Write(CreateLine());

        text.indent++;

        foreach (var statement in node.statements)
            DisplayNode(text, statement);

        text.indent--;
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));

        if (newLine)
            text.Write(CreateLine());
    }

    private static void DisplayTryStatement(DisplayText text, BoundTryStatement node) {
        text.Write(CreateKeyword(SyntaxKind.TryKeyword));
        text.Write(CreateSpace());
        DisplayBlockStatement(text, node.body, false);

        if (node.catchBody != null) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.CatchKeyword));
            text.Write(CreateSpace());
            DisplayBlockStatement(text, node.catchBody, false);
        }

        if (node.finallyBody != null) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.FinallyKeyword));
            text.Write(CreateSpace());
            DisplayBlockStatement(text, node.finallyBody, false);
        }

        text.Write(CreateLine());
    }

    private static void DisplayReturnStatement(DisplayText text, BoundReturnStatement node) {
        text.Write(CreateKeyword(SyntaxKind.ReturnKeyword));

        if (node.expression != null) {
            text.Write(CreateSpace());
            DisplayNode(text, node.expression);
        }

        text.Write(CreateLine());
    }

    private static void DisplayNestedStatement(DisplayText text, BoundStatement node) {
        var needsIndentation = node is not BoundBlockStatement;

        if (needsIndentation)
            text.indent++;

        DisplayNode(text, node);

        if (needsIndentation)
            text.indent--;
    }

    private static void DisplayDoWhileStatement(DisplayText text, BoundDoWhileStatement node) {
        text.Write(CreateKeyword(SyntaxKind.DoKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.Write(CreateLine());
        DisplayNestedStatement(text, node.body);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        text.Write(CreateSpace());
        text.Write(CreateKeyword(SyntaxKind.WhileKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.Write(CreateLine());
    }

    private static void DisplayConditionalGotoStatement(DisplayText text, BoundConditionalGotoStatement node) {
        text.Write(CreateKeyword("goto"));
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(node.label.name));
        text.Write(CreateSpace());
        text.Write(CreateKeyword(node.jumpIfTrue ? "if" : "unless"));
        text.Write(CreateSpace());
        DisplayNode(text, node.condition);
        text.Write(CreateLine());
    }

    private static void DisplayLabelStatement(DisplayText text, BoundLabelStatement node) {
        var unindent = text.indent > 0;
        if (unindent)
            text.indent--;

        text.Write(CreatePunctuation(node.label.name));
        text.Write(CreatePunctuation(SyntaxKind.ColonToken));
        text.Write(CreateLine());

        if (unindent)
            text.indent++;
    }

    private static void DisplayGotoStatement(DisplayText text, BoundGotoStatement node) {
        text.Write(CreateKeyword("goto"));
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(node.label.name));
        text.Write(CreateLine());
    }

    private static void DisplayForStatement(DisplayText text, BoundForStatement node) {
        text.Write(CreateKeyword(SyntaxKind.ForKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.initializer);
        text.Write(CreateSpace());
        DisplayNode(text, node.condition);
        text.Write(CreateSpace());
        DisplayNode(text, node.step);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.Write(CreateLine());
        DisplayNestedStatement(text, node.body);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        text.Write(CreateLine());
    }

    private static void DisplayWhileStatement(DisplayText text, BoundWhileStatement node) {
        text.Write(CreateKeyword(SyntaxKind.WhileKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.Write(CreateLine());
        DisplayNestedStatement(text, node.body);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        text.Write(CreateLine());
    }

    private static void DisplayIfStatement(DisplayText text, BoundIfStatement node) {
        text.Write(CreateKeyword(SyntaxKind.IfKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.Write(CreateLine());
        DisplayNestedStatement(text, node.then);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));

        if (node.elseStatement != null) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.ElseKeyword));
            text.Write(CreateSpace());
            text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
            text.Write(CreateLine());
            DisplayNestedStatement(text, node.elseStatement);
            text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        }

        text.Write(CreateLine());
    }

    private static void DisplayVariableDeclarationStatement(DisplayText text, BoundVariableDeclarationStatement node) {
        DisplayNode(text, node.variable.type);
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(node.variable.name));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.EqualsToken));
        text.Write(CreateSpace());
        DisplayNode(text, node.initializer);
        text.Write(CreateLine());
    }

    private static void DisplayExpressionStatement(DisplayText text, BoundExpressionStatement node) {
        if (node.expression is BoundEmptyExpression)
            return;

        DisplayNode(text, node.expression);
        text.Write(CreateLine());
    }

    private static void DisplayMemberAccessExpression(DisplayText text, BoundMemberAccessExpression node) {
        DisplayNode(text, node.operand);
        text.Write(CreatePunctuation(SyntaxKind.PeriodToken));
        text.Write(CreateIdentifier(node.member.name));
    }

    private static void DisplayConstructorExpression(DisplayText text, BoundConstructorExpression node) {
        SymbolDisplay.DisplaySymbol(text, node.symbol);
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayTernaryExpression(DisplayText text, BoundTernaryExpression node) {
        var precedence = SyntaxFacts.GetTernaryPrecedence(node.op.leftOpKind);

        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.left);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(node.op.leftOpKind));
        text.Write(CreateSpace());
        DisplayNode(text, node.center);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(node.op.rightOpKind));
        text.Write(CreateSpace());
        DisplayNode(text, node.right);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayTypeOfExpression(DisplayText text, BoundTypeOfExpression node) {
        text.Write(CreateKeyword(SyntaxKind.TypeOfKeyword));
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.typeOfType);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayIndexExpression(DisplayText text, BoundIndexExpression node) {
        DisplayNode(text, node.operand);
        text.Write(CreatePunctuation(SyntaxKind.OpenBracketToken));
        DisplayNode(text, node.index);
        text.Write(CreatePunctuation(SyntaxKind.CloseBracketToken));
    }

    private static void DisplayReferenceExpression(DisplayText text, BoundReferenceExpression node) {
        text.Write(CreateKeyword(SyntaxKind.RefKeyword));
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(node.variable.name));
    }

    private static void DisplayCastExpression(DisplayText text, BoundCastExpression node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.type);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        DisplayNode(text, node.expression);
    }

    private static void DisplayCallExpression(DisplayText text, BoundCallExpression node) {
        text.Write(CreateIdentifier(node.function.name));
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));

        var isFirst = true;
        foreach (var argument in node.arguments) {
            if (isFirst) {
                isFirst = false;
            } else {
                text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                text.Write(CreateSpace());
            }

            DisplayNode(text, argument);
        }

        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayInitializerListExpression(DisplayText text, BoundInitializerListExpression node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));

        var isFirst = true;

        foreach (var item in node.items) {
            if (isFirst) {
                isFirst = false;
            } else {
                text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                text.Write(CreateSpace());
            }

            DisplayNode(text, item);
        }

        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
    }

    private static void DisplayErrorExpression(DisplayText text, BoundErrorExpression node) {
        // This has no connection to SyntaxKind.QuestionToken, so the string literal is used here
        text.Write(CreateKeyword("?"));
    }

    private static void DisplayEmptyExpression(DisplayText text, BoundEmptyExpression node) { }

    private static void DisplayAssignmentExpression(DisplayText text, BoundAssignmentExpression node) {
        DisplayNode(text, node.left);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.EqualsToken));
        text.Write(CreateSpace());
        DisplayNode(text, node.right);
    }

    private static void DisplayVariableExpression(DisplayText text, BoundVariableExpression node) {
        SymbolDisplay.DisplaySymbol(text, node.variable);
    }

    private static void DisplayBinaryExpression(DisplayText text, BoundBinaryExpression node) {
        var precedence = SyntaxFacts.GetBinaryPrecedence(node.op.kind);

        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.left);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(node.op.kind));
        text.Write(CreateSpace());
        DisplayNode(text, node.right);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayLiteralExpression(DisplayText text, BoundLiteralExpression node) {
        if (node.value == null) {
            text.Write(CreateKeyword(SyntaxKind.NullKeyword));
            return;
        }

        var value = node.value.ToString();

        if (node.type.typeSymbol == TypeSymbol.Bool) {
            text.Write(CreateKeyword(value));
        } else if (node.type.typeSymbol == TypeSymbol.Int) {
            text.Write(CreateNumber(value));
        } else if (node.type.typeSymbol == TypeSymbol.String) {
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
            text.Write(CreateString(value));
        } else if (node.type.typeSymbol == TypeSymbol.Decimal) {
            text.Write(CreateNumber(value));
        } else {
            throw new BelteInternalException($"WriteLiteralExpression: unexpected type '{node.type.typeSymbol}'");
        }
    }

    private static void DisplayUnaryExpression(DisplayText text, BoundUnaryExpression node) {
        var precedence = SyntaxFacts.GetUnaryPrecedence(node.op.kind);

        text.Write(CreatePunctuation(node.op.kind));
        DisplayNode(text, node.operand);
    }
}
