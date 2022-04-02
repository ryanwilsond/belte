using System;
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
        GotoStatement,
        LabelStatement,
        ConditionalGotoStatement,
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

        public void WriteTo(TextWriter writer) {
            PrettyPrint(writer, this);
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
}
