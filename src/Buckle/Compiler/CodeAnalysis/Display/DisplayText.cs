using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Represents a piece of text with classifications for segments of text.
/// Can be multiple lines.
/// </summary>
public sealed class DisplayText {
    internal readonly List<DisplayTextSegment> segments;

    private bool _writeIndent = true;

    public DisplayText() {
        segments = [];
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

        foreach (var segment in segments)
            builder.Append(segment.text);

        return builder.ToString();
    }

    /// <summary>
    /// Returns the contents of this, and then clears this.
    /// </summary>
    /// <returns>The contents before clearing.</returns>
    public ImmutableArray<DisplayTextSegment> Flush() {
        var array = ImmutableArray.CreateRange(segments);
        segments.Clear();

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
                segments.Add(CreateIndent());
        }

        segments.Add(segment);

        if (segment.classification == Classification.Line)
            _writeIndent = true;
    }

    public void WriteLine() => Write(CreateLine());

    /// <summary>
    /// Appends multiple <see cref="DisplayTextSegment" /> to the end of this <see cref="DisplayText" />.
    /// </summary>
    /// <param name="segments"><see cref="DisplayTextSegment" />s to append.</param>
    public void Write(IEnumerable<DisplayTextSegment> segments) {
        foreach (var segment in segments)
            Write(segment);
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
        if (node is BoundExpression be && be.constantValue is not null) {
            DisplayConstant(text, be.constantValue);
            return;
        }

        switch (node.kind) {
            case BoundKind.NopStatement:
                DisplayNopStatement(text);
                break;
            case BoundKind.BlockStatement:
                DisplayBlockStatement(text, (BoundBlockStatement)node);
                break;
            case BoundKind.ExpressionStatement:
                DisplayExpressionStatement(text, (BoundExpressionStatement)node);
                break;
            case BoundKind.LocalDeclarationStatement:
                DisplayLocalDeclarationStatement(text, (BoundLocalDeclarationStatement)node);
                break;
            case BoundKind.IfStatement:
                DisplayIfStatement(text, (BoundIfStatement)node);
                break;
            case BoundKind.WhileStatement:
                DisplayWhileStatement(text, (BoundWhileStatement)node);
                break;
            case BoundKind.ForStatement:
                DisplayForStatement(text, (BoundForStatement)node);
                break;
            case BoundKind.GotoStatement:
                DisplayGotoStatement(text, (BoundGotoStatement)node);
                break;
            case BoundKind.LabelStatement:
                DisplayLabelStatement(text, (BoundLabelStatement)node);
                break;
            case BoundKind.ConditionalGotoStatement:
                DisplayConditionalGotoStatement(text, (BoundConditionalGotoStatement)node);
                break;
            case BoundKind.DoWhileStatement:
                DisplayDoWhileStatement(text, (BoundDoWhileStatement)node);
                break;
            case BoundKind.ReturnStatement:
                DisplayReturnStatement(text, (BoundReturnStatement)node);
                break;
            case BoundKind.TryStatement:
                DisplayTryStatement(text, (BoundTryStatement)node);
                break;
            case BoundKind.TypeExpression:
                DisplayTypeExpression(text, (BoundTypeExpression)node);
                break;
            case BoundKind.BreakStatement:
                DisplayBreakStatement(text);
                break;
            case BoundKind.ContinueStatement:
                DisplayContinueStatement(text);
                break;
            case BoundKind.GlobalStatement:
                DisplayGlobalStatement(text, (BoundGlobalStatement)node);
                break;
            case BoundKind.ArrayAccessExpression:
                DisplayArrayAccessExpression(text, (BoundArrayAccessExpression)node);
                break;
            case BoundKind.ReferenceExpression:
                DisplayReferenceExpression(text, (BoundReferenceExpression)node);
                break;
            case BoundKind.UnconvertedInitializerList:
                DisplayUnconvertedInitializerList(text, (BoundUnconvertedInitializerList)node);
                break;
            case BoundKind.UnaryOperator:
                DisplayUnaryOperator(text, (BoundUnaryOperator)node);
                break;
            case BoundKind.IncrementOperator:
                DisplayIncrementOperator(text, (BoundIncrementOperator)node);
                break;
            case BoundKind.InitializerList:
                DisplayInitializerList(text, (BoundInitializerList)node);
                break;
            case BoundKind.InitializerDictionary:
                DisplayInitializerDictionary(text, (BoundInitializerDictionary)node);
                break;
            case BoundKind.BinaryOperator:
                DisplayBinaryOperator(text, (BoundBinaryOperator)node);
                break;
            case BoundKind.DataContainerExpression:
                DisplayDataContainerExpression(text, (BoundDataContainerExpression)node);
                break;
            case BoundKind.AssignmentOperator:
                DisplayAssignmentOperator(text, (BoundAssignmentOperator)node);
                break;
            case BoundKind.CompoundAssignmentOperator:
                DisplayCompoundAssignmentOperator(text, (BoundCompoundAssignmentOperator)node);
                break;
            case BoundKind.ErrorExpression:
                DisplayErrorExpression(text, (BoundErrorExpression)node);
                break;
            case BoundKind.CallExpression:
                DisplayCallExpression(text, (BoundCallExpression)node);
                break;
            case BoundKind.CastExpression:
                DisplayCastExpression(text, (BoundCastExpression)node);
                break;
            case BoundKind.TypeOfExpression:
                DisplayTypeOfExpression(text, (BoundTypeOfExpression)node);
                break;
            case BoundKind.ObjectCreationExpression:
                DisplayObjectCreationExpression(text, (BoundObjectCreationExpression)node);
                break;
            case BoundKind.ArrayCreationExpression:
                DisplayArrayCreationExpression(text, (BoundArrayCreationExpression)node);
                break;
            case BoundKind.FieldAccessExpression:
                DisplayFieldAccessExpression(text, (BoundFieldAccessExpression)node);
                break;
            case BoundKind.ConditionalAccessExpression:
                DisplayConditionalAccessExpression(text, (BoundConditionalAccessExpression)node);
                break;
            case BoundKind.ThisExpression:
                DisplayThisExpression(text);
                break;
            case BoundKind.BaseExpression:
                DisplayBaseExpression(text);
                break;
            case BoundKind.ThrowExpression:
                DisplayThrowExpression(text, (BoundThrowExpression)node);
                break;
            case BoundKind.AsOperator:
                DisplayAsOperator(text, (BoundAsOperator)node);
                break;
            case BoundKind.IsOperator:
                DisplayIsOperator(text, (BoundIsOperator)node);
                break;
            case BoundKind.NullCoalescingOperator:
                DisplayNullCoalescingOperator(text, (BoundNullCoalescingOperator)node);
                break;
            case BoundKind.NullCoalescingAssignmentOperator:
                DisplayNullCoalescingAssignmentOperator(text, (BoundNullCoalescingAssignmentOperator)node);
                break;
            case BoundKind.NullAssertOperator:
                DisplayNullAssertOperator(text, (BoundNullAssertOperator)node);
                break;
            case BoundKind.ConditionalOperator:
                DisplayConditionalOperator(text, (BoundConditionalOperator)node);
                break;
            case BoundKind.DataContainerDeclaration:
                DisplayDataContainerDeclaration(text, (BoundDataContainerDeclaration)node);
                break;
            case BoundKind.FieldEqualsValue:
                DisplayFieldEqualsValue(text, (BoundFieldEqualsValue)node);
                break;
            case BoundKind.ParameterExpression:
                DisplayParameterExpression(text, (BoundParameterExpression)node);
                break;
            case BoundKind.ParameterEqualsValue:
                DisplayParameterEqualsValue(text, (BoundParameterEqualsValue)node);
                break;
            case BoundKind.TemplateParameterEqualsValue:
                DisplayTemplateParameterEqualsValue(text, (BoundTemplateParameterEqualsValue)node);
                break;
            case BoundKind.MethodGroup:
                DisplayMethodGroup(text, (BoundMethodGroup)node);
                break;
            case BoundKind.LocalFunctionStatement:
                DisplayLocalFunctionStatement(text, (BoundLocalFunctionStatement)node);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    /// <summary>
    /// Renders a <see cref="ConstantValue" /> and appends it to the given <see cref="DisplayText" />.
    /// </summary>
    internal static void DisplayConstant(DisplayText text, ConstantValue constant) {
        if (constant.value is ImmutableArray<ConstantValue> il) {
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
            DisplayLiteralExpressionCore(text, constant.value);
        }
    }

    internal static void DisplayConstant(DisplayText text, object value) {
        DisplayLiteralExpressionCore(text, value);
    }

    /// <summary>
    /// Formats a literal into a string representation.
    /// </summary>
    internal static string FormatLiteral(object value) {
        var text = new DisplayText();
        DisplayLiteralExpressionCore(text, value);
        return text.ToString();
    }

    private static void DisplayLiteralExpressionCore(DisplayText text, object value) {
        if (value is null) {
            text.Write(CreateLiteral("null"));
            return;
        }

        var specialType = LiteralUtilities.AssumeTypeFromLiteral(value);

        if (specialType == SpecialType.String)
            DisplayStringLiteral(value.ToString(), false);
        else if (specialType == SpecialType.Char)
            DisplayStringLiteral(value.ToString(), true);
        else
            text.Write(CreateLiteral(value.ToString().ToLower()));

        void DisplayStringLiteral(string value, bool isCharacter) {
            var stringBuilder = new StringBuilder(isCharacter ? "'" : "\"");

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

            stringBuilder.Append(isCharacter ? '\'' : '"');
            text.Write(CreateString(stringBuilder.ToString()));
        }
    }

    private static void DisplayTypeExpression(DisplayText text, BoundTypeExpression node) {
        SymbolDisplay.DisplayType(text, node.type);
    }

    private static void DisplayMethodGroup(DisplayText text, BoundMethodGroup node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBracketToken));
        text.Write(CreateSpace());
        SymbolDisplay.AppendToDisplayText(text, node.methods[0], SymbolDisplayFormat.DebuggerDisplay);
        text.Write(CreateSpace());
        text.Write(CreateLiteral(node.methods.Length.ToString()));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.CloseBracketToken));
    }

    private static void DisplayBreakStatement(DisplayText text) {
        text.Write(CreateKeyword(SyntaxKind.BreakKeyword));
        text.WriteLine();
    }

    private static void DisplayContinueStatement(DisplayText text) {
        text.Write(CreateKeyword(SyntaxKind.ContinueKeyword));
        text.WriteLine();
    }

    private static void DisplayGlobalStatement(DisplayText text, BoundGlobalStatement node) {
        DisplayNode(text, node.statement);
    }

    private static void DisplayNopStatement(DisplayText text) {
        text.Write(CreateKeyword("nop"));
        text.WriteLine();
    }

    private static void DisplayBlockStatement(DisplayText text, BoundBlockStatement node, bool newLine = true) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.WriteLine();

        text.indent++;

        foreach (var statement in node.statements)
            DisplayNode(text, statement);

        text.indent--;
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));

        if (newLine)
            text.WriteLine();
    }

    private static void DisplayTryStatement(DisplayText text, BoundTryStatement node) {
        text.Write(CreateKeyword(SyntaxKind.TryKeyword));
        text.Write(CreateSpace());
        DisplayBlockStatement(text, (BoundBlockStatement)node.body, false);

        if (node.catchBody is not null) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.CatchKeyword));
            text.Write(CreateSpace());
            DisplayBlockStatement(text, (BoundBlockStatement)node.catchBody, false);
        }

        if (node.finallyBody is not null) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.FinallyKeyword));
            text.Write(CreateSpace());
            DisplayBlockStatement(text, (BoundBlockStatement)node.finallyBody, false);
        }

        text.WriteLine();
    }

    private static void DisplayReturnStatement(DisplayText text, BoundReturnStatement node) {
        text.Write(CreateKeyword(SyntaxKind.ReturnKeyword));

        if (node.expression is not null) {
            text.Write(CreateSpace());
            DisplayNode(text, node.expression);
        }

        text.WriteLine();
    }

    private static void DisplayLocalFunctionStatement(DisplayText text, BoundLocalFunctionStatement node) {
        SymbolDisplay.AppendToDisplayText(text, node.symbol);
        text.WriteLine();
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
        text.WriteLine();
        DisplayNestedStatement(text, node.body);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        text.Write(CreateSpace());
        text.Write(CreateKeyword(SyntaxKind.WhileKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.WriteLine();
    }

    private static void DisplayConditionalGotoStatement(DisplayText text, BoundConditionalGotoStatement node) {
        text.Write(CreateKeyword("goto"));
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(node.label.name));
        text.Write(CreateSpace());
        text.Write(CreateKeyword(node.jumpIfTrue ? "if" : "unless"));
        text.Write(CreateSpace());
        DisplayNode(text, node.condition);
        text.WriteLine();
    }

    private static void DisplayLabelStatement(DisplayText text, BoundLabelStatement node) {
        var unindent = text.indent > 0;
        if (unindent)
            text.indent--;

        text.Write(CreatePunctuation(node.label.name));
        text.Write(CreatePunctuation(SyntaxKind.ColonToken));
        text.WriteLine();

        if (unindent)
            text.indent++;
    }

    private static void DisplayGotoStatement(DisplayText text, BoundGotoStatement node) {
        text.Write(CreateKeyword("goto"));
        text.Write(CreateSpace());
        text.Write(CreateIdentifier(node.label.name));
        text.WriteLine();
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
        text.WriteLine();
        DisplayNestedStatement(text, node.body);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        text.WriteLine();
    }

    private static void DisplayWhileStatement(DisplayText text, BoundWhileStatement node) {
        text.Write(CreateKeyword(SyntaxKind.WhileKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.WriteLine();
        DisplayNestedStatement(text, node.body);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        text.WriteLine();
    }

    private static void DisplayIfStatement(DisplayText text, BoundIfStatement node) {
        text.Write(CreateKeyword(SyntaxKind.IfKeyword));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
        text.WriteLine();
        DisplayNestedStatement(text, node.consequence);
        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));

        if (node.alternative is not null) {
            text.Write(CreateSpace());
            text.Write(CreateKeyword(SyntaxKind.ElseKeyword));
            text.Write(CreateSpace());
            text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
            text.WriteLine();
            DisplayNestedStatement(text, node.alternative);
            text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
        }

        text.WriteLine();
    }

    private static void DisplayLocalDeclarationStatement(DisplayText text, BoundLocalDeclarationStatement node) {
        DisplayNode(text, node.declaration);
    }

    private static void DisplayDataContainerDeclaration(DisplayText text, BoundDataContainerDeclaration node) {
        var dataContainer = node.dataContainer;

        SymbolDisplay.AppendToDisplayText(text, dataContainer, SymbolDisplayFormat.BoundDisplayFormat);

        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.EqualsToken));
        text.Write(CreateSpace());

        if (dataContainer.isRef) {
            text.Write(CreateKeyword(SyntaxKind.RefKeyword));
            text.Write(CreateSpace());
        }

        DisplayNode(text, node.initializer);
        text.WriteLine();
    }

    private static void DisplayExpressionStatement(DisplayText text, BoundExpressionStatement node) {
        DisplayNode(text, node.expression);
        text.WriteLine();
    }

    private static void DisplayFieldAccessExpression(
        DisplayText text,
        BoundFieldAccessExpression node,
        bool conditional = false) {
        DisplayNode(text, node.receiver);
        text.Write(CreatePunctuation(conditional ? SyntaxKind.QuestionPeriodToken : SyntaxKind.PeriodToken));
        text.Write(CreateIdentifier(node.field.name));
    }

    private static void DisplayArrayAccessExpression(
        DisplayText text,
        BoundArrayAccessExpression node,
        bool conditional = false) {
        DisplayNode(text, node.receiver);
        text.Write(CreatePunctuation(conditional ? SyntaxKind.QuestionOpenBracketToken : SyntaxKind.OpenBracketToken));
        DisplayNode(text, node.index);
        text.Write(CreatePunctuation(SyntaxKind.CloseBracketToken));
    }

    private static void DisplayConditionalAccessExpression(DisplayText text, BoundConditionalAccessExpression node) {
        DisplayNode(text, node.receiver);
        var accessExpression = node.accessExpression;

        switch (accessExpression) {
            case BoundArrayAccessExpression a:
                DisplayArrayAccessExpression(text, a, true);
                break;
            case BoundFieldAccessExpression f:
                DisplayFieldAccessExpression(text, f, true);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(accessExpression.kind);
        }
    }

    private static void DisplayObjectCreationExpression(DisplayText text, BoundObjectCreationExpression node) {
        text.Write(CreateKeyword(SyntaxKind.NewKeyword));
        text.Write(CreateSpace());
        SymbolDisplay.DisplayType(text, node.type, SymbolDisplayFormat.ObjectCreationFormat);
        DisplayArguments(text, node.arguments);
    }

    private static void DisplayArrayCreationExpression(DisplayText text, BoundArrayCreationExpression node) {
        text.Write(CreateKeyword(SyntaxKind.NewKeyword));
        text.Write(CreateSpace());
        SymbolDisplay.DisplayType(text, node.type, SymbolDisplayFormat.BoundDisplayFormat);

        if (node.initializer is not null) {
            text.Write(CreateSpace());
            DisplayNode(text, node.initializer);
        }
    }

    private static void DisplayThisExpression(DisplayText text) {
        text.Write(CreateKeyword(SyntaxKind.ThisKeyword));
    }

    private static void DisplayBaseExpression(DisplayText text) {
        text.Write(CreateKeyword(SyntaxKind.BaseKeyword));
    }

    private static void DisplayThrowExpression(DisplayText text, BoundThrowExpression node) {
        text.Write(CreateKeyword(SyntaxKind.ThrowKeyword));
        text.Write(CreateSpace());
        DisplayNode(text, node.expression);
    }

    private static void DisplayConditionalOperator(DisplayText text, BoundConditionalOperator node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, node.condition);

        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.QuestionToken));
        text.Write(CreateSpace());

        if (node.isRef) {
            text.Write(CreateKeyword(SyntaxKind.RefKeyword));
            text.Write(CreateSpace());
        }

        DisplayNode(text, node.trueExpression);

        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.ColonToken));
        text.Write(CreateSpace());

        if (node.isRef) {
            text.Write(CreateKeyword(SyntaxKind.RefKeyword));
            text.Write(CreateSpace());
        }

        DisplayNode(text, node.falseExpression);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayTypeOfExpression(DisplayText text, BoundTypeOfExpression node) {
        text.Write(CreateKeyword(SyntaxKind.TypeOfKeyword));
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        SymbolDisplay.DisplayType(text, node.type);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayReferenceExpression(DisplayText text, BoundReferenceExpression node) {
        text.Write(CreateKeyword(SyntaxKind.RefKeyword));
        text.Write(CreateSpace());
        DisplayNode(text, node.expression);
    }

    private static void DisplayCastExpression(DisplayText text, BoundCastExpression node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        SymbolDisplay.DisplayType(text, node.type);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
        DisplayNode(text, node.operand);
    }

    private static void DisplayCallExpression(DisplayText text, BoundCallExpression node) {
        if (node.receiver is not null) {
            DisplayNode(text, node.receiver);
            text.Write(CreatePunctuation(SyntaxKind.PeriodToken));
            text.Write(CreateIdentifier(node.method.name));
        } else {
            // Static methods drop their receiver (it's functionally not used)
            // but we want to add it back here for clarity
            SymbolDisplay.AppendToDisplayText(text, node.method, SymbolDisplayFormat.QualifiedNameFormat);
        }

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

    private static void DisplayUnconvertedInitializerList(DisplayText text, BoundUnconvertedInitializerList node) {
        DisplayListCore(text, node.items);
    }

    private static void DisplayInitializerList(DisplayText text, BoundInitializerList node) {
        DisplayListCore(text, node.items);
    }

    private static void DisplayListCore(DisplayText text, ImmutableArray<BoundExpression> items) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));

        var isFirst = true;

        foreach (var item in items) {
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

    private static void DisplayInitializerDictionary(DisplayText text, BoundInitializerDictionary node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));

        var isFirst = true;

        foreach (var item in node.items) {
            if (isFirst) {
                isFirst = false;
            } else {
                text.Write(CreatePunctuation(SyntaxKind.CommaToken));
                text.Write(CreateSpace());
            }

            DisplayNode(text, item.Item1);
            text.Write(CreatePunctuation(SyntaxKind.ColonToken));
            text.Write(CreateSpace());
            DisplayNode(text, item.Item2);
        }

        text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
    }

    private static void DisplayErrorExpression(DisplayText text, BoundErrorExpression node) {
        text.Write(CreatePunctuation(SyntaxKind.OpenBracketToken));
        text.Write(CreateKeyword(SyntaxKind.QuestionToken));
        text.Write(CreateSpace());
        SymbolDisplay.DisplayType(text, node.type, SymbolDisplayFormat.Everything);
        text.Write(CreatePunctuation(SyntaxKind.CloseBracketToken));
    }

    private static void DisplayAssignmentOperator(DisplayText text, BoundAssignmentOperator node) {
        DisplayNode(text, node.left);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.EqualsToken));
        text.Write(CreateSpace());

        if (node.isRef) {
            text.Write(CreateKeyword(SyntaxKind.RefKeyword));
            text.Write(CreateSpace());
        }

        DisplayNode(text, node.right);
    }

    private static void DisplayBinaryAdjacentExpression(
        DisplayText text,
        BoundExpression left,
        BoundExpression right,
        SyntaxKind op,
        bool isKeywordOp) {
        text.Write(CreatePunctuation(SyntaxKind.OpenParenToken));
        DisplayNode(text, left);
        text.Write(CreateSpace());
        text.Write(isKeywordOp ? CreateKeyword(op) : CreatePunctuation(op));
        text.Write(CreateSpace());
        DisplayNode(text, right);
        text.Write(CreatePunctuation(SyntaxKind.CloseParenToken));
    }

    private static void DisplayBinaryOperator(DisplayText text, BoundBinaryOperator node) {
        var opKind = node.operatorKind == BinaryOperatorKind.Error
            ? SyntaxKind.QuestionToken
            : node.operatorKind.ToSyntaxKind();

        DisplayBinaryAdjacentExpression(text, node.left, node.right, opKind, false);
    }

    private static void DisplayCompoundAssignmentOperator(DisplayText text, BoundCompoundAssignmentOperator node) {
        var opKind = node.op.kind == BinaryOperatorKind.Error
            ? SyntaxKind.QuestionToken
            : SyntaxFacts.GetAssignmentOperatorOfBinaryOperator(node.op.kind.ToSyntaxKind());

        DisplayBinaryAdjacentExpression(text, node.left, node.right, opKind, false);
    }

    private static void DisplayIsOperator(DisplayText text, BoundIsOperator node) {
        var op = node.isNot ? SyntaxKind.IsntKeyword : SyntaxKind.IsKeyword;
        DisplayBinaryAdjacentExpression(text, node.left, node.right, op, true);
    }

    private static void DisplayAsOperator(DisplayText text, BoundAsOperator node) {
        DisplayBinaryAdjacentExpression(text, node.left, node.right, SyntaxKind.AsKeyword, true);
    }

    private static void DisplayNullCoalescingOperator(DisplayText text, BoundNullCoalescingOperator node) {
        DisplayBinaryAdjacentExpression(text, node.left, node.right, SyntaxKind.QuestionQuestionToken, false);
    }

    private static void DisplayNullCoalescingAssignmentOperator(
        DisplayText text,
        BoundNullCoalescingAssignmentOperator node) {
        DisplayBinaryAdjacentExpression(text, node.left, node.right, SyntaxKind.QuestionQuestionEqualsToken, false);
    }

    private static void DisplayDataContainerExpression(DisplayText text, BoundDataContainerExpression node) {
        text.Write(CreateIdentifier(node.dataContainer.name));
    }

    private static void DisplayParameterExpression(DisplayText text, BoundParameterExpression node) {
        text.Write(CreateIdentifier(node.parameter.name));
    }

    private static void DisplayUnaryOperator(DisplayText text, BoundUnaryOperator node) {
        text.Write(CreatePunctuation(node.operatorKind.ToSyntaxKind()));
        DisplayNode(text, node.operand);
    }

    private static void DisplayIncrementOperator(DisplayText text, BoundIncrementOperator node) {
        if (node.operatorKind.Operator() is UnaryOperatorKind.PrefixDecrement or UnaryOperatorKind.PrefixIncrement)
            text.Write(CreatePunctuation(node.operatorKind.ToSyntaxKind()));

        DisplayNode(text, node.operand);

        if (node.operatorKind.Operator() is UnaryOperatorKind.PostfixDecrement or UnaryOperatorKind.PostfixIncrement)
            text.Write(CreatePunctuation(node.operatorKind.ToSyntaxKind()));
    }

    private static void DisplayNullAssertOperator(DisplayText text, BoundNullAssertOperator node) {
        DisplayNode(text, node.operand);
        text.Write(CreatePunctuation(SyntaxKind.ExclamationToken));
    }

    private static void DisplayFieldEqualsValue(DisplayText text, BoundFieldEqualsValue node) {
        DisplayEqualsValueCore(text, node.field, node.value);
    }

    private static void DisplayParameterEqualsValue(DisplayText text, BoundParameterEqualsValue node) {
        DisplayEqualsValueCore(text, node.parameter, node.value);
    }

    private static void DisplayTemplateParameterEqualsValue(DisplayText text, BoundTemplateParameterEqualsValue node) {
        DisplayEqualsValueCore(text, node.parameter, node.value);
    }

    private static void DisplayEqualsValueCore(DisplayText text, Symbol symbol, BoundExpression value) {
        SymbolDisplay.AppendToDisplayText(text, symbol, SymbolDisplayFormat.BoundDisplayFormat);
        text.Write(CreateSpace());
        text.Write(CreatePunctuation(SyntaxKind.EqualsToken));
        text.Write(CreateSpace());
        DisplayNode(text, value);
        text.WriteLine();
    }
}
