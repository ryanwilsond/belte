using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

namespace Buckle.CodeAnalysis.Binding {

    internal enum BoundNodeType {
        Invalid,
        UnaryExpression,
        LiteralExpression,
        BinaryExpression,
        VariableExpression,
        AssignmentExpression,
        EmptyExpression,
        BlockStatement,
        ExpressionStatement,
        VariableDeclarationStatement,
        IfStatement,
        WhileStatement,
        ForStatement,
    }

    internal abstract class BoundNode {
        public abstract BoundNodeType type { get; }

        public IEnumerable<BoundNode> GetChildren() {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties) {
                if (typeof(BoundNode).IsAssignableFrom(property.PropertyType)) {
                    var child = (BoundNode)property.GetValue(this);
                    if (child != null)
                        yield return child;
                } else if (typeof(IEnumerable<BoundNode>).IsAssignableFrom(property.PropertyType)) {
                    var values = (IEnumerable<BoundNode>)property.GetValue(this);
                    foreach (var child in values) {
                        if (child != null)
                            yield return child;
                    }
                }
            }
        }

        private IEnumerable<(string name, object value)> GetProperties() {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties) {
                if (property.Name == nameof(type) || property.Name == nameof(BoundBinaryExpression.op))
                    continue;
                if (typeof(BoundNode).IsAssignableFrom(property.PropertyType) ||
                    typeof(IEnumerable<BoundNode>).IsAssignableFrom(property.PropertyType))
                    continue;

                var value = property.GetValue(this);
                if (value != null) {
                    string name = property.Name;
                    if (name == "lType") name = "type";

                    yield return (name, value);
                }
            }
        }

        public void WriteTo(TextWriter writer, bool isLast=false) {
            PrettyPrint(writer, this, isLast);
        }

        private ConsoleColor GetColor(BoundNode node) {
            if (node is BoundExpression) return ConsoleColor.DarkBlue;
            if (node is BoundStatement) return ConsoleColor.Cyan;
            return ConsoleColor.Yellow;
        }

        private void PrettyPrint(TextWriter writer, BoundNode node, bool isLast=true, string indent="")
        {
            bool isConsoleOut = writer == Console.Out;
            string marker = isLast ? "└─" : "├─";

            if (isConsoleOut)
                Console.ForegroundColor = ConsoleColor.DarkGray;

            writer.Write($"{indent}{marker}");

            if (isConsoleOut)
                Console.ForegroundColor = GetColor(node);

            string text = GetText(node);
            writer.Write(text);

            bool isFirstProperty = true;

            foreach (var p in node.GetProperties()) {
                if (isFirstProperty) {
                    isFirstProperty = false;
                } else {
                    if (isConsoleOut)
                        Console.ForegroundColor = ConsoleColor.DarkGray;

                    writer.Write(",");
                }

                writer.Write(" ");

                if (isConsoleOut)
                    Console.ForegroundColor = ConsoleColor.Yellow;

                writer.Write(p.name);

                if (isConsoleOut)
                    Console.ForegroundColor = ConsoleColor.DarkGray;

                writer.Write(" = ");

                if (isConsoleOut)
                    Console.ForegroundColor = ConsoleColor.DarkYellow;

                writer.Write(p.value);
            }

            writer.WriteLine();

            if (isConsoleOut)
                Console.ResetColor();

            indent += isLast ? "  " : "│ ";
            var lastChild = node.GetChildren().LastOrDefault();

            foreach (var child in node.GetChildren())
                PrettyPrint(writer, child, child == lastChild, indent);
        }

        private string GetText(BoundNode node) {
            if (node is BoundBinaryExpression b)
                return b.op.opType.ToString() + "Expression";
            if (node is BoundUnaryExpression u)
                return u.op.opType.ToString() + "Expression";

            return node.type.ToString();
        }

        public override string ToString() {
            using (var writer = new StringWriter()) {
                WriteTo(writer);
                return writer.ToString();
            }
        }
    }

    internal abstract class BoundExpression : BoundNode {
        public abstract Type lType { get; }
    }

    internal sealed class BoundLiteralExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.LiteralExpression;
        public override Type lType => value.GetType();
        public object value { get; }

        public BoundLiteralExpression(object value_) {
            value = value_;
        }
    }

    internal sealed class BoundVariableExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public override Type lType => variable.lType;
        public override BoundNodeType type => BoundNodeType.VariableExpression;

        public BoundVariableExpression(VariableSymbol variable_) {
            variable = variable_;
        }
    }

    internal sealed class BoundAssignmentExpression : BoundExpression {
        public VariableSymbol variable { get; }
        public BoundExpression expression { get; }
        public override BoundNodeType type => BoundNodeType.AssignmentExpression;
        public override Type lType => expression.lType;

        public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expression_) {
            variable = variable_;
            expression = expression_;
        }
    }

    internal sealed class BoundEmptyExpression : BoundExpression {
        public override BoundNodeType type => BoundNodeType.EmptyExpression;
        public override Type lType => null;

        public BoundEmptyExpression() {}
    }

    internal abstract class BoundStatement : BoundNode { }

    internal sealed class BoundBlockStatement : BoundStatement {
        public ImmutableArray<BoundStatement> statements { get; }
        public override BoundNodeType type => BoundNodeType.BlockStatement;

        public BoundBlockStatement(ImmutableArray<BoundStatement> statements_) {
            statements = statements_;
        }
    }

    internal sealed class BoundExpressionStatement : BoundStatement {
        public BoundExpression expression { get; }
        public override BoundNodeType type => BoundNodeType.ExpressionStatement;

        public BoundExpressionStatement(BoundExpression expression_) {
            expression = expression_;
        }
    }

    internal sealed class BoundVariableDeclarationStatement : BoundStatement {
        public VariableSymbol variable { get; }
        public BoundExpression initializer { get; }
        public override BoundNodeType type => BoundNodeType.VariableDeclarationStatement;

        public BoundVariableDeclarationStatement(VariableSymbol variable_, BoundExpression initializer_) {
            variable = variable_;
            initializer = initializer_;
        }
    }

    internal sealed class BoundIfStatement : BoundStatement {
        public BoundExpression condition { get; }
        public BoundStatement then { get; }
        public BoundStatement elseStatement { get; }
        public override BoundNodeType type => BoundNodeType.IfStatement;

        public BoundIfStatement(BoundExpression condition_, BoundStatement then_, BoundStatement elseStatement_) {
            condition = condition_;
            then = then_;
            elseStatement = elseStatement_;
        }
    }

    internal sealed class BoundWhileStatement : BoundStatement {
        public BoundExpression condition { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.WhileStatement;

        public BoundWhileStatement(BoundExpression condition_, BoundStatement body_) {
            condition = condition_;
            body = body_;
        }
    }

    internal sealed class BoundForStatement : BoundStatement {
        public BoundVariableDeclarationStatement stepper { get; }
        public BoundExpression condition { get; }
        public BoundAssignmentExpression step { get; }
        public BoundStatement body { get; }
        public override BoundNodeType type => BoundNodeType.ForStatement;

        public BoundForStatement(
            BoundVariableDeclarationStatement stepper_, BoundExpression condition_,
            BoundAssignmentExpression step_, BoundStatement body_) {
            stepper = stepper_;
            condition = condition_;
            step = step_;
            body = body_;
        }
    }
}
