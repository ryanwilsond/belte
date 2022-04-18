using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax {
    internal sealed partial class LiteralExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return token;
        }
    }

    internal sealed partial class BinaryExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return left;
            yield return op;
            yield return right;
        }
    }

    internal sealed partial class ParenthesisExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return openParenthesis;
            yield return expression;
            yield return closeParenthesis;
        }
    }

    internal sealed partial class UnaryExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return op;
            yield return operand;
        }
    }

    internal sealed partial class NameExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return identifier;
        }
    }

    internal sealed partial class AssignmentExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return identifier;
            yield return equals;
            yield return expression;
        }
    }

    internal sealed partial class EmptyExpression {
        public override IEnumerable<Node> GetChildren() {
            return Array.Empty<Node>();
        }
    }

    internal sealed partial class CallExpression {
        public override IEnumerable<Node> GetChildren() {
            yield return identifier;
            yield return openParenthesis;
            foreach (var child in arguments.GetWithSeparators()) {
                yield return child;
            }
            yield return closeParenthesis;
        }
    }

    internal sealed partial class Parameter {
        public override IEnumerable<Node> GetChildren() {
            yield return typeName;
            yield return identifier;
        }
    }

    internal sealed partial class FunctionDeclaration {
        public override IEnumerable<Node> GetChildren() {
            yield return typeName;
            yield return identifier;
            yield return openParenthesis;
            foreach (var child in parameters.GetWithSeparators()) {
                yield return child;
            }
            yield return closeParenthesis;
            yield return body;
        }
    }

    internal sealed partial class GlobalStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return statement;
        }
    }

    internal sealed partial class VariableDeclarationStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return typeName;
            yield return identifier;
            yield return equals;
            yield return initializer;
            yield return semicolon;
        }
    }

    internal sealed partial class BlockStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return openBrace;
            foreach (var child in statements) {
                yield return child;
            }
            yield return closeBrace;
        }
    }

    internal sealed partial class ExpressionStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return expression;
            yield return semicolon;
        }
    }

    internal sealed partial class IfStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return ifKeyword;
            yield return openParenthesis;
            yield return condition;
            yield return closeParenthesis;
            yield return then;
            yield return elseClause;
        }
    }

    internal sealed partial class ElseClause {
        public override IEnumerable<Node> GetChildren() {
            yield return elseKeyword;
            yield return then;
        }
    }

    internal sealed partial class WhileStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return keyword;
            yield return openParenthesis;
            yield return condition;
            yield return closeParenthesis;
            yield return body;
        }
    }

    internal sealed partial class ForStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return keyword;
            yield return openParenthesis;
            yield return initializer;
            yield return condition;
            yield return semicolon;
            yield return step;
            yield return closeParenthesis;
            yield return body;
        }
    }

    internal sealed partial class DoWhileStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return doKeyword;
            yield return body;
            yield return whileKeyword;
            yield return openParenthesis;
            yield return condition;
            yield return closeParenthesis;
            yield return semicolon;
        }
    }

    internal sealed partial class ContinueStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return keyword;
            yield return semicolon;
        }
    }

    internal sealed partial class BreakStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return keyword;
            yield return semicolon;
        }
    }

    internal sealed partial class ReturnStatement {
        public override IEnumerable<Node> GetChildren() {
            yield return keyword;
            yield return expression;
            yield return semicolon;
        }
    }

    internal sealed partial class CompilationUnit {
        public override IEnumerable<Node> GetChildren() {
            foreach (var child in members) {
                yield return child;
            }
            yield return endOfFile;
        }
    }

}
