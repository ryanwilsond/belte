using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security;
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
public sealed class DisplayText {
    private readonly List<DisplayTextSegment> _segments;

    private bool _writeIndent = true;

    public DisplayText() {
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

    /// <summary>
    /// Returns the contents of this, and then clears this.
    /// </summary>
    /// <returns>The contents before clearing.</returns>
    public ImmutableArray<DisplayTextSegment> Flush() {
        var array = ImmutableArray.CreateRange(_segments);
        _segments.Clear();

        return array;
    }

    /// <summary>
    /// Appends a <see cref="DisplayTextSegment" /> to the end of this <see cref="DisplayText" />.
    /// </summary>
    /// <param name="segment"><see cref="DisplayTextSegment" /> to append.</param>
    public void Write(DisplayTextSegment segment) {
        if (_writeIndent) {
            _writeIndent = false;

            for (var i = 0; i < indent; i++)
                _segments.Add(CreateIndent());
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
        if (node is BoundExpression be && be.constantValue != null) {
            DisplayConstant(text, be.constantValue);
            return;
        }

        switch (node.kind) {
            case BoundNodeKind.VariableDeclaration:
                DisplayVariableDeclaration(text, (BoundVariableDeclaration)node);
                break;
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
            case BoundNodeKind.LocalDeclarationStatement:
                DisplayLocalDeclarationStatement(text, (BoundLocalDeclarationStatement)node);
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
            case BoundNodeKind.LiteralExpression when node is BoundInitializerListExpression:
                DisplayInitializerListExpression(text, node as BoundInitializerListExpression);
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
            case BoundNodeKind.ObjectCreationExpression:
                DisplayObjectCreationExpression(text, (BoundObjectCreationExpression)node);
                break;
            case BoundNodeKind.MemberAccessExpression:
                DisplayMemberAccessExpression(text, (BoundMemberAccessExpression)node);
                break;
            case BoundNodeKind.ThisExpression:
                DisplayThisExpression(text, (BoundThisExpression)node);
                break;
            default:
                throw new BelteInternalException($"DisplayNode: unexpected node '{node.kind}'");
        }
    }

    /// <summary>
    /// Renders a <see cref="BoundConstant" /> and appends it to the given <see cref="DisplayText" />.
    /// </summary>
    internal static void DisplayConstant(DisplayText text, BoundConstant constant) {
        if (constant.value is ImmutableArray<BoundConstant> il) {
            text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
            var isFirst = true;

            foreach (var item in il) {
                if (isFirst) {
                    isFirst = false;
                } else {
                    text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                    text.Write(CreateSpace());
                }

                DisplayConstant(text, item);
            }

            text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        } else {
            DisplayLiteralExpression(text, new BoundLiteralExpression(constant.value));
        }
    }

    /// <summary>
    /// Formats a literal into a string representation.
    /// </summary>
    internal static string FormatLiteral(object value) {
        if (value is null)
            return "null";

        var valueText = value.ToString();
        var typeSymbol = BoundType.Assume(value).typeSymbol;

        if (typeSymbol == TypeSymbol.String)
            return FormatStringLiteral(valueText);
        else
            return valueText.ToLower();

        string FormatStringLiteral(string stringText) {
            var stringBuilder = new StringBuilder("\"");

            foreach (var c in stringText) {
                switch (c) {
                    case '\a':
                        stringBuilder.Append("\\a");
                        break;
                    case '\b':
                        stringBuilder.Append("\\b");
                        break;
                    case '\f':
                        stringBuilder.Append("\\f");
                        break;
                    case '\n':
                        stringBuilder.Append("\\n");
                        break;
                    case '\r':
                        stringBuilder.Append("\\r");
                        break;
                    case '\t':
                        stringBuilder.Append("\\t");
                        break;
                    case '\v':
                        stringBuilder.Append("\\v");
                        break;
                    case '\"':
                        stringBuilder.Append("\\\"");
                        break;
                    case '\\':
                        stringBuilder.Append("\\\\");
                        break;
                    default:
                        stringBuilder.Append(c);
                        break;
                }
            }

            stringBuilder.Append("\"");
            return stringBuilder.ToString();
        }
    }

    private static void DisplayType(DisplayText text, BoundType type) {
        if (type.isConstant) {
            text.Write(CreateKeyword(SyntaxKind.ConstKeyword));
            text.Write(CreateSpace());
        }

        if (type.isReference) {
            text.Write(CreateKeyword(SyntaxKind.RefKeyword));
            text.Write(CreateSpace());
        }

        if (type.isConstantReference) {
            text.Write(CreateKeyword(SyntaxKind.ConstKeyword));
            text.Write(CreateSpace());
        }

        text.Write(CreateType(type.typeSymbol.name));

        if (type.arity > 0) {
            text.Write(CreatePunctuation(SyntaxKind.LessThanToken));

            var isFirst = true;

            foreach (var argument in type.templateArguments) {
                if (isFirst) {
                    isFirst = false;
                } else {
                    text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                    text.Write(CreateSpace());
                }

                if (argument.isConstant)
                    DisplayConstant(text, argument.constant);
                else
                    DisplayNode(text, argument.type);
            }

            text.Write(CreatePunctuation(SyntaxKind.GreaterThanToken));
        }

        for (var i = 0; i < type.dimensions; i++) {
            text.Write(CreatePunctuation(SyntaxKind.OpenBracketToken));

            if (type.sizes.Length > i)
                DisplayNode(text, type.sizes[i]);

            text.Write(CreatePunctuation(SyntaxKind.CloseBracketToken));
        }

        if (!type.isNullable && !type.isLiteral && type.typeSymbol != TypeSymbol.Void)
            text.Write(CreatePunctuation(SyntaxKind.ExclamationToken));
    }

    private static void DisplayNopStatement(DisplayText text, BoundNopStatement _) {
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

    private static void DisplayLocalDeclarationStatement(DisplayText text, BoundLocalDeclarationStatement node) {
        DisplayNode(text, node.declaration);
    }

    private static void DisplayVariableDeclaration(DisplayText text, BoundVariableDeclaration node) {
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
        DisplayNode(text, node.left);
        text.Write(CreatePunctuation(SyntaxKind.PeriodToken));
        DisplayNode(text, node.right);
    }

    private static void DisplayObjectCreationExpression(DisplayText text, BoundObjectCreationExpression node) {
        text.Write(CreateKeyword(SyntaxKind.NewKeyword));
        text.Write(CreateSpace());
        DisplayNode(text, node.type);

        if (node.viaConstructor)
            DisplayArguments(text, node.arguments);
    }

    private static void DisplayThisExpression(DisplayText text, BoundThisExpression _) {
        text.Write(CreateKeyword(SyntaxKind.ThisKeyword));
    }

    private static void DisplayTernaryExpression(DisplayText text, BoundTernaryExpression node) {
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
        DisplayNode(text, node.expression);
        text.Write(CreatePunctuation(SyntaxKind.OpenBracketToken));
        DisplayNode(text, node.index);
        text.Write(CreatePunctuation(SyntaxKind.CloseBracketToken));
    }

    private static void DisplayReferenceExpression(DisplayText text, BoundReferenceExpression node) {
        text.Write(CreateKeyword(SyntaxKind.RefKeyword));
        text.Write(CreateSpace());
        DisplayNode(text, node.expression);
    }

    private static void DisplayCastExpression(DisplayText text, BoundCastExpression node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.type);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        DisplayNode(text, node.expression);
    }

    private static void DisplayCallExpression(DisplayText text, BoundCallExpression node) {
        if (node.expression is not BoundEmptyExpression) {
            DisplayNode(text, node.expression);
            text.Write(CreatePunctuation(SyntaxKind.PeriodToken));
        }

        text.Write(CreateIdentifier(node.method.name));
        DisplayArguments(text, node.arguments);
    }

    private static void DisplayArguments(DisplayText text, ImmutableArray<BoundExpression> arguments) {
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));

        var isFirst = true;
        foreach (var argument in arguments) {
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

    private static void DisplayErrorExpression(DisplayText text, BoundErrorExpression _) {
        // This has no connection to SyntaxKind.QuestionToken, so the string literal is used here
        text.Write(CreateKeyword("?"));
    }

    private static void DisplayEmptyExpression(DisplayText _, BoundEmptyExpression _1) { }

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
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.left);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(node.op.kind));
        text.Write(CreateSpace());
        DisplayNode(text, node.right);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayLiteralExpression(DisplayText text, BoundLiteralExpression node) {
        if (node.value is null) {
            text.Write(CreateLiteral("null"));
            return;
        }

        var value = node.value.ToString();
        var typeSymbol = BoundType.Assume(node.value).typeSymbol;

        if (typeSymbol == TypeSymbol.String)
            DisplayStringLiteral(value);
        else
            text.Write(CreateLiteral(value.ToLower()));

        void DisplayStringLiteral(string value) {
            var stringBuilder = new StringBuilder("\"");

            foreach (var c in value) {
                switch (c) {
                    case '\a':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\a"));
                        break;
                    case '\b':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\b"));
                        break;
                    case '\f':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\f"));
                        break;
                    case '\n':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\n"));
                        break;
                    case '\r':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\r"));
                        break;
                    case '\t':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\t"));
                        break;
                    case '\v':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\v"));
                        break;
                    case '\"':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\\""));
                        break;
                    case '\\':
                        text.Write(CreateString(stringBuilder.ToString()));
                        stringBuilder.Clear();
                        text.Write(CreateEscape("\\\\"));
                        break;
                    default:
                        stringBuilder.Append(c);
                        break;
                }
            }

            stringBuilder.Append("\"");
            text.Write(CreateString(stringBuilder.ToString()));
        }
    }

    private static void DisplayUnaryExpression(DisplayText text, BoundUnaryExpression node) {
        text.Write(CreatePunctuation(node.op.kind));
        DisplayNode(text, node.operand);
    }
}
