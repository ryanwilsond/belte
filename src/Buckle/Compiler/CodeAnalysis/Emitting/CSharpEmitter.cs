using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Shared;

namespace Buckle.CodeAnalysis.Emitting;

/// <summary>
/// Emits a bound program into a C# source.
/// </summary>
internal sealed class CSharpEmitter {
    private bool _insideMain;

    /// <summary>
    /// Emits a program to a C# source.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <returns>Diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(BoundProgram program, string outputPath) {
        if (program.diagnostics.Errors().Any())
            return program.diagnostics;

        var stringWriter = Emit(program, Path.GetFileNameWithoutExtension(outputPath), out var diagnostics);

        using (var writer = new StreamWriter(outputPath))
            writer.Write(stringWriter);

        return diagnostics;
    }

    /// <summary>
    /// Emits a program to a string.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="namespaceName">
    /// Name of the namespace that will contain the program; prevents symbol contamination.
    /// </param>
    /// <param name="diagnostics">Any produced diagnostics.</param>
    /// <returns>C# source as a string.</returns>
    internal static string Emit(
        BoundProgram program, string namespaceName, out BelteDiagnosticQueue diagnostics) {
        var emitter = new CSharpEmitter();

        return emitter.EmitInternal(program, namespaceName, out diagnostics);
    }

    private string EmitInternal(BoundProgram program, string namespaceName, out BelteDiagnosticQueue diagnostics) {
        var stringWriter = new StringWriter();
        var indentString = "    ";

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
            indentedTextWriter.WriteLine("using System;");
            indentedTextWriter.WriteLine("using System.Collections.Generic;");
            indentedTextWriter.WriteLine();
            indentedTextWriter.WriteLine($"namespace {GetSafeName(namespaceName)};");
            indentedTextWriter.WriteLine();

            using (var programClassCurly = new CurlyIndenter(indentedTextWriter, "public static class Program")) {
                indentedTextWriter.WriteLine();

                foreach (var @struct in program.types.Where(t => t is StructSymbol))
                    EmitStruct(indentedTextWriter, @struct as StructSymbol);

                if (program.entryPoint != null) {
                    var mainBody = MethodUtilities.LookupMethod(program.methodBodies, program.entryPoint);
                    EmitMainMethod(indentedTextWriter, KeyValuePair.Create(program.entryPoint, mainBody));
                } else {
                    EmitEmptyMainMethod(indentedTextWriter);
                }

                foreach (var methodWithBody in program.methodBodies) {
                    if (methodWithBody.Key != program.entryPoint)
                        EmitMethod(indentedTextWriter, methodWithBody);
                }
            }

            indentedTextWriter.Flush();
        }

        stringWriter.Flush();
        diagnostics = new BelteDiagnosticQueue();

        var stringBuilder = new StringBuilder();

        foreach (var line in stringWriter.ToString().Split(Environment.NewLine)) {
            stringBuilder.Append(line.TrimEnd());
            stringBuilder.Append(Environment.NewLine);
        }

        // Adds a final trailing new line
        return stringBuilder.ToString().TrimEnd() + Environment.NewLine;
    }

    private string GetEquivalentType(BoundType type, bool makeReferenceExplicit = false) {
        string GetEquivalentTypeName(TypeSymbol typeSymbol) {
            if (typeSymbol is StructSymbol)
                return GetSafeName(typeSymbol.name);

            if (typeSymbol == TypeSymbol.Bool)
                return "bool";
            if (typeSymbol == TypeSymbol.Decimal)
                return "double";
            if (typeSymbol == TypeSymbol.Int)
                return "int";
            if (typeSymbol == TypeSymbol.String)
                return "string";

            return "object";
        }

        if (type.typeSymbol == TypeSymbol.Void)
            return "void";

        var equivalentType = new StringBuilder();
        var typeName = GetEquivalentTypeName(type.typeSymbol);

        // All logic relating to constants has already been handled by the Binder,
        // so specifying const here would not do anything
        if (type.isExplicitReference || (type.isReference && makeReferenceExplicit))
            equivalentType.Append("ref ");
        if (type.isNullable && new List<string>() { "bool", "double", "int" }.Contains(typeName))
            typeName = $"Nullable<{typeName}>";

        for (var i = 0; i < type.dimensions; i++)
            typeName = $"List<{typeName}>";

        equivalentType.Append(typeName);

        return equivalentType.ToString();
    }

    private string GetSafeName(string name) {
        var provider = CodeDomProvider.CreateProvider("C#");
        return (provider.IsValidIdentifier(name) ? name : "@" + name)
            .Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }

    private void EmitStruct(IndentedTextWriter indentedTextWriter, StructSymbol @struct) {
        var signature = $"public class {GetSafeName(@struct.name)}";

        using (var structCurly = new CurlyIndenter(indentedTextWriter, signature)) {
            foreach (var field in @struct.members.OfType<FieldSymbol>())
                EmitField(indentedTextWriter, field);
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitField(IndentedTextWriter indentedTextWriter, FieldSymbol field) {
        indentedTextWriter.WriteLine($"public {GetEquivalentType(field.type)} {GetSafeName(field.name)};");
    }

    private void EmitMainMethod(
        IndentedTextWriter indentedTextWriter, KeyValuePair<MethodSymbol, BoundBlockStatement> method) {
        var typeName = method.Key.type.typeSymbol == TypeSymbol.Void ? "void" : "int";
        var signature = $"public static {typeName} Main()";

        using (var methodCurly = new CurlyIndenter(indentedTextWriter, signature)) {
            _insideMain = true;
            EmitBody(indentedTextWriter, method.Value);
            _insideMain = false;
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitEmptyMainMethod(IndentedTextWriter indentedTextWriter) {
        indentedTextWriter.WriteLine("public static void Main() { }");
        indentedTextWriter.WriteLine();
    }

    private void EmitMethod(
        IndentedTextWriter indentedTextWriter, KeyValuePair<MethodSymbol, BoundBlockStatement> method) {
        var parameters = new StringBuilder();
        var isFirst = true;

        foreach (var parameter in method.Key.parameters) {
            if (isFirst)
                isFirst = false;
            else
                parameters.Append(", ");

            parameters.Append($"{GetEquivalentType(parameter.type)} {GetSafeName(parameter.name)}");
        }

        var signature =
            $"public static {GetEquivalentType(method.Key.type)} {GetSafeName(method.Key.name)}({parameters})";

        using (var methodCurly = new CurlyIndenter(indentedTextWriter, signature))
            EmitBody(indentedTextWriter, method.Value);

        indentedTextWriter.WriteLine();
    }

    private void EmitBody(IndentedTextWriter indentedTextWriter, BoundBlockStatement body) {
        foreach (var statement in body.statements)
            EmitStatement(indentedTextWriter, statement);
    }

    private void EmitStatement(IndentedTextWriter indentedTextWriter, BoundStatement statement) {
        switch (statement.kind) {
            case BoundNodeKind.BlockStatement:
                EmitBody(indentedTextWriter, (BoundBlockStatement)statement);
                break;
            case BoundNodeKind.NopStatement:
                EmitNopStatement(indentedTextWriter, (BoundNopStatement)statement);
                break;
            case BoundNodeKind.ExpressionStatement:
                EmitExpressionStatement(indentedTextWriter, (BoundExpressionStatement)statement);
                break;
            case BoundNodeKind.VariableDeclarationStatement:
                EmitVariableDeclarationStatement(indentedTextWriter, (BoundVariableDeclarationStatement)statement);
                break;
            case BoundNodeKind.GotoStatement:
                EmitGotoStatement(indentedTextWriter, (BoundGotoStatement)statement);
                break;
            case BoundNodeKind.LabelStatement:
                EmitLabelStatement(indentedTextWriter, (BoundLabelStatement)statement);
                break;
            case BoundNodeKind.ConditionalGotoStatement:
                EmitConditionalGotoStatement(indentedTextWriter, (BoundConditionalGotoStatement)statement);
                break;
            case BoundNodeKind.ReturnStatement:
                EmitReturnStatement(indentedTextWriter, (BoundReturnStatement)statement);
                break;
            case BoundNodeKind.TryStatement:
                EmitTryStatement(indentedTextWriter, (BoundTryStatement)statement);
                break;
            case BoundNodeKind.IfStatement:
                EmitIfStatement(indentedTextWriter, (BoundIfStatement)statement);
                break;
            case BoundNodeKind.ForStatement:
                EmitForStatement(indentedTextWriter, (BoundForStatement)statement);
                break;
            case BoundNodeKind.WhileStatement:
                EmitWhileStatement(indentedTextWriter, (BoundWhileStatement)statement);
                break;
            case BoundNodeKind.DoWhileStatement:
                EmitDoWhileStatement(indentedTextWriter, (BoundDoWhileStatement)statement);
                break;
            case BoundNodeKind.BreakStatement:
                EmitBreakStatement(indentedTextWriter, (BoundBreakStatement)statement);
                break;
            case BoundNodeKind.ContinueStatement:
                EmitContinueStatement(indentedTextWriter, (BoundContinueStatement)statement);
                break;
            default:
                throw new BelteInternalException($"EmitStatement: unexpected node '{statement.kind}'");
        }
    }

    private void EmitNopStatement(IndentedTextWriter indentedTextWriter, BoundNopStatement statement) {
        indentedTextWriter.WriteLine(";");
    }

    private void EmitExpressionStatement(IndentedTextWriter indentedTextWriter, BoundExpressionStatement statement) {
        if (statement.expression is not BoundEmptyExpression) {
            EmitExpression(indentedTextWriter, statement.expression);
            indentedTextWriter.WriteLine(";");
        }
    }

    private void EmitVariableDeclarationStatement(
        IndentedTextWriter indentedTextWriter, BoundVariableDeclarationStatement statement) {
        indentedTextWriter.Write(GetEquivalentType(statement.variable.type, true));
        indentedTextWriter.Write($" {GetSafeName(statement.variable.name)}");

        if (statement.initializer != null) {
            indentedTextWriter.Write(" = ");
            EmitExpression(indentedTextWriter, statement.initializer);
        }

        indentedTextWriter.WriteLine(";");
    }


    private void EmitGotoStatement(IndentedTextWriter indentedTextWriter, BoundGotoStatement statement) {
        indentedTextWriter.WriteLine($"goto {statement.label.name};");
    }

    private void EmitLabelStatement(IndentedTextWriter indentedTextWriter, BoundLabelStatement statement) {
        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine($"{statement.label.name}:");
        indentedTextWriter.Indent++;
    }

    private void EmitConditionalGotoStatement(
        IndentedTextWriter indentedTextWriter, BoundConditionalGotoStatement statement) {
        indentedTextWriter.Write($"if ((");

        if (statement.jumpIfTrue)
            indentedTextWriter.Write("(");
        else
            indentedTextWriter.Write("!(");

        EmitExpression(indentedTextWriter, statement.condition);

        indentedTextWriter.WriteLine(")) ?? throw new NullReferenceException())");
        indentedTextWriter.Indent++;
        indentedTextWriter.WriteLine($"goto {statement.label.name};");
        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine();
    }

    private void EmitReturnStatement(IndentedTextWriter indentedTextWriter, BoundReturnStatement statement) {
        if (statement.expression is null) {
            indentedTextWriter.WriteLine("return;");
        } else {
            indentedTextWriter.Write("return ");

            if (BoundConstant.IsNull(statement.expression.constantValue)) {
                indentedTextWriter.WriteLine("0;");
                return;
            }

            EmitExpression(indentedTextWriter, statement.expression);

            if (_insideMain && statement.expression.type.isNullable)
                indentedTextWriter.WriteLine(" ?? 0;");
            else
                indentedTextWriter.WriteLine(";");
        }
    }

    private void EmitTryStatement(IndentedTextWriter indentedTextWriter, BoundTryStatement statement) {
        using (var tryCurly = new CurlyIndenter(indentedTextWriter, "try"))
            EmitBody(indentedTextWriter, statement.body);

        if (statement.catchBody != null) {
            using (var catchCurly = new CurlyIndenter(indentedTextWriter, "catch"))
                EmitBody(indentedTextWriter, statement.catchBody);
        }

        if (statement.finallyBody != null) {
            using (var finallyCurly = new CurlyIndenter(indentedTextWriter, "finally"))
                EmitBody(indentedTextWriter, statement.finallyBody);
        }
    }

    private void EmitNullProtectedExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        if (expression.type.isNullable)
            indentedTextWriter.Write("(");

        EmitExpression(indentedTextWriter, expression);

        if (expression.type.isNullable)
            indentedTextWriter.Write(" ?? throw new NullReferenceException())");
    }

    private void EmitIfStatement(IndentedTextWriter indentedTextWriter, BoundIfStatement statement) {
        indentedTextWriter.Write("if (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);

        using (var ifCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.then);

        if (statement.elseStatement != null) {
            using (var elseCurly = new CurlyIndenter(indentedTextWriter, "else"))
                EmitStatement(indentedTextWriter, statement.elseStatement);
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitForStatement(IndentedTextWriter indentedTextWriter, BoundForStatement statement) {
        indentedTextWriter.Write("for (");
        EmitStatement(indentedTextWriter, statement.initializer);
        indentedTextWriter.Indent++;
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);
        indentedTextWriter.Write("; ");
        EmitExpression(indentedTextWriter, statement.step);
        indentedTextWriter.Indent--;

        using (var forCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.body);

        indentedTextWriter.WriteLine();
    }

    private void EmitWhileStatement(IndentedTextWriter indentedTextWriter, BoundWhileStatement statement) {
        indentedTextWriter.Write("while (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);

        using (var forCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.body);

        indentedTextWriter.WriteLine();
    }

    private void EmitDoWhileStatement(IndentedTextWriter indentedTextWriter, BoundDoWhileStatement statement) {
        using (var forCurly = new CurlyIndenter(indentedTextWriter, "do"))
            EmitStatement(indentedTextWriter, statement.body);

        indentedTextWriter.Write("while (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);
        indentedTextWriter.WriteLine(");");
        indentedTextWriter.WriteLine();
    }

    private void EmitBreakStatement(IndentedTextWriter indentedTextWriter, BoundBreakStatement statement) {
        indentedTextWriter.WriteLine("break;");
    }

    private void EmitContinueStatement(IndentedTextWriter indentedTextWriter, BoundContinueStatement statement) {
        indentedTextWriter.WriteLine("continue;");
    }

    private void EmitExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        if (expression.constantValue != null) {
            EmitConstantExpression(indentedTextWriter, expression);
            return;
        }

        switch (expression.kind) {
            case BoundNodeKind.LiteralExpression:
                if (expression is BoundInitializerListExpression il) {
                    EmitInitializerListExpression(indentedTextWriter, il);
                    break;
                } else {
                    goto default;
                }
            case BoundNodeKind.UnaryExpression:
                EmitUnaryExpression(indentedTextWriter, (BoundUnaryExpression)expression);
                break;
            case BoundNodeKind.BinaryExpression:
                EmitBinaryExpression(indentedTextWriter, (BoundBinaryExpression)expression);
                break;
            case BoundNodeKind.VariableExpression:
                EmitVariableExpression(indentedTextWriter, (BoundVariableExpression)expression);
                break;
            case BoundNodeKind.AssignmentExpression:
                EmitAssignmentExpression(indentedTextWriter, (BoundAssignmentExpression)expression);
                break;
            case BoundNodeKind.EmptyExpression:
                EmitEmptyExpression(indentedTextWriter, (BoundEmptyExpression)expression);
                break;
            case BoundNodeKind.CallExpression:
                EmitCallExpression(indentedTextWriter, (BoundCallExpression)expression);
                break;
            case BoundNodeKind.IndexExpression:
                EmitIndexExpression(indentedTextWriter, (BoundIndexExpression)expression);
                break;
            case BoundNodeKind.CastExpression:
                EmitCastExpression(indentedTextWriter, (BoundCastExpression)expression);
                break;
            case BoundNodeKind.TernaryExpression:
                EmitTernaryExpression(indentedTextWriter, (BoundTernaryExpression)expression);
                break;
            case BoundNodeKind.PrefixExpression:
                EmitPrefixExpression(indentedTextWriter, (BoundPrefixExpression)expression);
                break;
            case BoundNodeKind.PostfixExpression:
                EmitPostfixExpression(indentedTextWriter, (BoundPostfixExpression)expression);
                break;
            case BoundNodeKind.CompoundAssignmentExpression:
                EmitCompoundAssignmentExpression(indentedTextWriter, (BoundCompoundAssignmentExpression)expression);
                break;
            case BoundNodeKind.ReferenceExpression:
                EmitReferenceExpression(indentedTextWriter, (BoundReferenceExpression)expression);
                break;
            case BoundNodeKind.ObjectCreationExpression:
                EmitObjectCreationExpression(indentedTextWriter, (BoundObjectCreationExpression)expression);
                break;
            case BoundNodeKind.MemberAccessExpression:
                EmitMemberAccessExpression(indentedTextWriter, (BoundMemberAccessExpression)expression);
                break;
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private void EmitConstantExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        EmitBoundConstant(indentedTextWriter, expression.constantValue, expression.type);
    }

    private void EmitBoundConstant(IndentedTextWriter indentedTextWriter, BoundConstant constant, BoundType type) {
        if (constant.value is ImmutableArray<BoundConstant> ia) {
            indentedTextWriter.Write($"new {GetEquivalentType(type)} {{ ");

            var isFirst = true;

            foreach (var item in ia) {
                if (isFirst)
                    isFirst = false;
                else
                    indentedTextWriter.Write(", ");

                EmitBoundConstant(indentedTextWriter, item, type.ChildType());
            }

            indentedTextWriter.Write(" }");
        } else {
            if (BoundConstant.IsNull(constant))
                indentedTextWriter.Write("null");
            else if (constant.value is bool)
                indentedTextWriter.Write(constant.value.ToString().ToLower());
            else
                indentedTextWriter.Write(constant.value);
        }
    }

    private void EmitInitializerListExpression(
        IndentedTextWriter indentedTextWriter, BoundInitializerListExpression expression) {
        indentedTextWriter.Write($"new {GetEquivalentType(expression.type)} {{ ");

        var isFirst = true;

        foreach (var item in expression.items) {
            if (isFirst)
                isFirst = false;
            else
                indentedTextWriter.Write(", ");

            EmitExpression(indentedTextWriter, item);
        }

        indentedTextWriter.Write(" }");
    }

    private void EmitUnaryExpression(IndentedTextWriter indentedTextWriter, BoundUnaryExpression expression) {
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
    }

    private void EmitBinaryExpression(IndentedTextWriter indentedTextWriter, BoundBinaryExpression expression) {
        indentedTextWriter.Write("(");
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)} ");
        EmitExpression(indentedTextWriter, expression.right);
        indentedTextWriter.Write(")");
    }

    private void EmitVariableExpression(IndentedTextWriter indentedTextWriter, BoundVariableExpression expression) {
        indentedTextWriter.Write(GetSafeName(expression.variable.name));
    }

    private void EmitAssignmentExpression(
        IndentedTextWriter indentedTextWriter, BoundAssignmentExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write(" = ");
        EmitExpression(indentedTextWriter, expression.right);
    }

    private void EmitEmptyExpression(IndentedTextWriter indentedTextWriter, BoundEmptyExpression expression) { }

    private void EmitCallExpression(IndentedTextWriter indentedTextWriter, BoundCallExpression expression) {
        string methodName = null;

        switch (expression.method.name) {
            case "Print":
                methodName = "Console.Write";
                break;
            case "PrintLine":
                methodName = "Console.WriteLine";
                break;
            case "Input":
                methodName = "Console.ReadLine";
                break;
            case "RandInt":
                var signature = $"Func<{GetEquivalentType(expression.type)}>";
                indentedTextWriter.Write(
                    $"(({signature})(() => {{ var random = new System.Random(); var temp = "
                );

                EmitExpression(indentedTextWriter, expression.arguments[0]);
                indentedTextWriter.Write("; ");

                if (expression.arguments[0].type.isNullable)
                    indentedTextWriter.Write("return temp.HasValue ? random.Next(temp.Value) : random.Next();");
                else
                    indentedTextWriter.Write("return random.Next(temp);");

                indentedTextWriter.Write(" }))()");

                return;
            case "Value":
                EmitExpression(indentedTextWriter, expression.arguments[0]);

                if (GetEquivalentType(expression.arguments[0].type).StartsWith("Nullable"))
                    indentedTextWriter.Write(".Value");

                return;
            case "HasValue":
                if (GetEquivalentType(expression.arguments[0].type).StartsWith("Nullable")) {
                    EmitExpression(indentedTextWriter, expression.arguments[0]);
                    indentedTextWriter.Write(".HasValue");
                } else if (expression.arguments[0].type.isNullable) {
                    EmitExpression(indentedTextWriter, expression.arguments[0]);
                    indentedTextWriter.Write($" is not null");
                } else {
                    indentedTextWriter.Write("true");
                }

                return;
        }

        indentedTextWriter.Write($"{methodName ?? GetSafeName(expression.method.name)}");
        EmitArguments(indentedTextWriter, expression.arguments);
    }

    private void EmitArguments(IndentedTextWriter indentedTextWriter, ImmutableArray<BoundExpression> arguments) {
        indentedTextWriter.Write("(");

        var isFirst = true;

        foreach (var argument in arguments) {
            if (isFirst)
                isFirst = false;
            else
                indentedTextWriter.Write(", ");

            EmitExpression(indentedTextWriter, argument);
        }

        indentedTextWriter.Write(")");
    }

    private void EmitIndexExpression(IndentedTextWriter indentedTextWriter, BoundIndexExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write("[");
        EmitExpression(indentedTextWriter, expression.index);
        indentedTextWriter.Write("]");
    }

    private void EmitCastExpression(IndentedTextWriter indentedTextWriter, BoundCastExpression expression) {
        if (expression.type.isNullable)
            indentedTextWriter.Write($"({GetEquivalentType(expression.type)})");

        var neededParenthesis = 1;
        var typeSymbol = expression.type.typeSymbol;

        if (typeSymbol == TypeSymbol.Bool) {
            indentedTextWriter.Write("Convert.ToBoolean(");
        } else if (typeSymbol == TypeSymbol.Decimal) {
            indentedTextWriter.Write("Convert.ToDouble(");
        } else if (typeSymbol == TypeSymbol.String) {
            indentedTextWriter.Write("Convert.ToString(");
        } else if (typeSymbol == TypeSymbol.Int) {
            indentedTextWriter.Write("Convert.ToInt32(");

            if (expression.expression.type.typeSymbol == TypeSymbol.Decimal) {
                indentedTextWriter.Write("Math.Truncate(");
                neededParenthesis = 2;
            }
        } else {
            neededParenthesis = 0;
        }

        EmitExpression(indentedTextWriter, expression.expression);
        indentedTextWriter.Write(new string(')', neededParenthesis));
    }

    private void EmitTernaryExpression(IndentedTextWriter indentedTextWriter, BoundTernaryExpression expression) {
        indentedTextWriter.Write("(");

        if (expression.left.type.isNullable)
            indentedTextWriter.Write("((");

        EmitExpression(indentedTextWriter, expression.left);

        if (expression.left.type.isNullable)
            indentedTextWriter.Write(") ?? throw new NullReferenceException())");

        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.leftOpKind)} ");

        if (BoundConstant.IsNull(expression.right.constantValue)) {
            indentedTextWriter.Write("(");
            indentedTextWriter.Write(GetEquivalentType(expression.center.type));
            indentedTextWriter.Write(")");
        }

        EmitExpression(indentedTextWriter, expression.center);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.rightOpKind)} ");

        if (BoundConstant.IsNull(expression.center.constantValue)) {
            indentedTextWriter.Write("(");
            indentedTextWriter.Write(GetEquivalentType(expression.center.type));
            indentedTextWriter.Write(")");
        }

        EmitExpression(indentedTextWriter, expression.right);
        indentedTextWriter.Write(")");
    }

    private void EmitPrefixExpression(IndentedTextWriter indentedTextWriter, BoundPrefixExpression expression) {
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
    }

    private void EmitPostfixExpression(IndentedTextWriter indentedTextWriter, BoundPostfixExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
    }

    private void EmitCompoundAssignmentExpression(
        IndentedTextWriter indentedTextWriter, BoundCompoundAssignmentExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)}= ");
        EmitExpression(indentedTextWriter, expression.right);
    }

    private void EmitReferenceExpression(IndentedTextWriter indentedTextWriter, BoundReferenceExpression expression) {
        indentedTextWriter.Write($"ref {GetSafeName(expression.variable.name)}");
    }

    private void EmitObjectCreationExpression(
        IndentedTextWriter indentedTextWriter, BoundObjectCreationExpression expression) {
        indentedTextWriter.Write($"new {GetEquivalentType(expression.type)}");
        EmitArguments(indentedTextWriter, expression.arguments);
    }

    private void EmitMemberAccessExpression(
        IndentedTextWriter indentedTextWriter, BoundMemberAccessExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write($".{GetSafeName(expression.member.name)}");
    }
}
