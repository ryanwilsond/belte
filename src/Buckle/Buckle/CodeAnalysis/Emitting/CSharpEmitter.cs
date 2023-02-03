using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Generators;
using Buckle.Utilities;
using Diagnostics;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CSharpEmitter {
    /// <summary>
    /// Emits a program to a C# source.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <returns>Diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(BoundProgram program, string outputPath) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
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
        var stringWriter = new StringWriter();
        string indentString = "    ";

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
            indentedTextWriter.WriteLine("using System;");
            indentedTextWriter.WriteLine("using System.Collections.Generic;");
            indentedTextWriter.WriteLine();
            indentedTextWriter.WriteLine($"namespace {GetSafeName(namespaceName)};");
            indentedTextWriter.WriteLine();

            using (var programClassCurly = new CurlyIndenter(indentedTextWriter, "public static class Program")) {
                indentedTextWriter.WriteLine();

                foreach (var structStructure in program.structMembers)
                    EmitStruct(indentedTextWriter, structStructure);

                if (program.mainFunction != null) {
                    var mainBody = FunctionUtilities.LookupMethod(program.functionBodies, program.mainFunction);
                    EmitMainMethod(indentedTextWriter, KeyValuePair.Create(program.mainFunction, mainBody));
                } else {
                    EmitEmptyMainMethod(indentedTextWriter);
                }

                foreach (var functionWithBody in program.functionBodies) {
                    if (!functionWithBody.Key.MethodMatches(program.mainFunction))
                        EmitMethod(indentedTextWriter, functionWithBody);
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

    private static string GetEquivalentType(BoundType type) {
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

        // All logic relating to constants has already been handled by the Binder, so specifying const here would
        // not do anything
        if (type.isReference)
            equivalentType.Append("ref ");
        if (type.isNullable && new List<String>() {"bool", "double", "int"}.Contains(typeName))
            typeName = $"Nullable<{typeName}>";

        for (int i=0; i<type.dimensions; i++)
            typeName = $"List<{typeName}>";

        equivalentType.Append(typeName);

        return equivalentType.ToString();
    }

    private static string GetSafeName(string name) {
        CodeDomProvider provider = CodeDomProvider.CreateProvider("C#");
        return (provider.IsValidIdentifier(name) ? name : "@" + name)
            .Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }

    private static void EmitStruct(
        IndentedTextWriter indentedTextWriter, KeyValuePair<StructSymbol, ImmutableList<FieldSymbol>> structure) {
        var signature = $"public class {GetSafeName(structure.Key.name)}";

        using (var structCurly = new CurlyIndenter(indentedTextWriter, signature)) {
            foreach (var field in structure.Value)
                EmitField(indentedTextWriter, field);
        }

        indentedTextWriter.WriteLine();
    }

    private static void EmitField(IndentedTextWriter indentedTextWriter, FieldSymbol field) {
        indentedTextWriter.WriteLine($"public {GetEquivalentType(field.type)} {GetSafeName(field.name)};");
    }

    private static void EmitMainMethod(
        IndentedTextWriter indentedTextWriter, KeyValuePair<FunctionSymbol, BoundBlockStatement> method) {
        var typeName = method.Key.type.typeSymbol == TypeSymbol.Void ? "void" : "int";
        var signature = $"public static {typeName} Main()";

        using (var methodCurly = new CurlyIndenter(indentedTextWriter, signature))
            EmitBody(indentedTextWriter, method.Value, true);

        indentedTextWriter.WriteLine();
    }

    private static void EmitEmptyMainMethod(IndentedTextWriter indentedTextWriter) {
        indentedTextWriter.WriteLine("public static void Main() { }");
        indentedTextWriter.WriteLine();
    }

    private static void EmitMethod(
        IndentedTextWriter indentedTextWriter, KeyValuePair<FunctionSymbol, BoundBlockStatement> method) {
        StringBuilder parameters = new StringBuilder();
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

    private static void EmitBody(
        IndentedTextWriter indentedTextWriter, BoundBlockStatement body, bool insideMain = false) {
        foreach (var statement in body.statements)
            EmitStatement(indentedTextWriter, statement, insideMain);
    }

    private static void EmitStatement(
        IndentedTextWriter indentedTextWriter, BoundStatement statement, bool insideMain) {
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
                EmitReturnStatement(indentedTextWriter, (BoundReturnStatement)statement, insideMain);
                break;
            case BoundNodeKind.TryStatement:
                EmitTryStatement(indentedTextWriter, (BoundTryStatement)statement, insideMain);
                break;
            case BoundNodeKind.IfStatement:
                EmitIfStatement(indentedTextWriter, (BoundIfStatement)statement, insideMain);
                break;
            case BoundNodeKind.ForStatement:
                EmitForStatement(indentedTextWriter, (BoundForStatement)statement, insideMain);
                break;
            case BoundNodeKind.WhileStatement:
                EmitWhileStatement(indentedTextWriter, (BoundWhileStatement)statement, insideMain);
                break;
            case BoundNodeKind.DoWhileStatement:
                EmitDoWhileStatement(indentedTextWriter, (BoundDoWhileStatement)statement, insideMain);
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

    private static void EmitNopStatement(
        IndentedTextWriter indentedTextWriter, BoundNopStatement statement) {
        indentedTextWriter.WriteLine(";");
    }

    private static void EmitExpressionStatement(
        IndentedTextWriter indentedTextWriter, BoundExpressionStatement statement) {
        if (statement.expression is not BoundEmptyExpression) {
            EmitExpression(indentedTextWriter, statement.expression);
            indentedTextWriter.WriteLine(";");
        }
    }

    private static void EmitVariableDeclarationStatement(
        IndentedTextWriter indentedTextWriter, BoundVariableDeclarationStatement statement) {
        indentedTextWriter.Write(GetEquivalentType(statement.variable.type));
        indentedTextWriter.Write($" {GetSafeName(statement.variable.name)}");

        if (statement.initializer != null) {
            indentedTextWriter.Write(" = ");
            EmitExpression(indentedTextWriter, statement.initializer);
        }

        indentedTextWriter.WriteLine(";");
    }


    private static void EmitGotoStatement(IndentedTextWriter indentedTextWriter, BoundGotoStatement statement) {
        indentedTextWriter.WriteLine($"goto {statement.label.name};");
    }

    private static void EmitLabelStatement(IndentedTextWriter indentedTextWriter, BoundLabelStatement statement) {
        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine($"{statement.label.name}:");
        indentedTextWriter.Indent++;
    }

    private static void EmitConditionalGotoStatement(
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

    private static void EmitReturnStatement(
        IndentedTextWriter indentedTextWriter, BoundReturnStatement statement, bool insideMain) {
        if (statement.expression == null) {
            indentedTextWriter.WriteLine("return;");
        } else {
            indentedTextWriter.Write("return (");
            EmitExpression(indentedTextWriter, statement.expression);

            if (insideMain)
                indentedTextWriter.WriteLine(") ?? 0;");
            else
                indentedTextWriter.WriteLine(");");
        }
    }

    private static void EmitTryStatement(
        IndentedTextWriter indentedTextWriter, BoundTryStatement statement, bool insideMain) {
        using (var tryCurly = new CurlyIndenter(indentedTextWriter, "try"))
            EmitBody(indentedTextWriter, statement.body, insideMain);

        if (statement.catchBody != null) {
            using (var catchCurly = new CurlyIndenter(indentedTextWriter, "catch"))
                EmitBody(indentedTextWriter, statement.catchBody, insideMain);
        }

        if (statement.finallyBody != null) {
            using (var finallyCurly = new CurlyIndenter(indentedTextWriter, "finally"))
                EmitBody(indentedTextWriter, statement.finallyBody, insideMain);
        }
    }

    private static void EmitNullProtectedExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        if (expression.type.isNullable)
            indentedTextWriter.Write("((");

        EmitExpression(indentedTextWriter, expression);

        if (expression.type.isNullable)
            indentedTextWriter.Write(") ?? throw new NullReferenceException())");
    }

    private static void EmitIfStatement(
        IndentedTextWriter indentedTextWriter, BoundIfStatement statement, bool insideMain) {
        indentedTextWriter.Write("if (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);

        using (var ifCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.then, insideMain);

        if (statement.elseStatement != null) {
            using (var elseCurly = new CurlyIndenter(indentedTextWriter, "else"))
                EmitStatement(indentedTextWriter, statement.elseStatement, insideMain);
        }

        indentedTextWriter.WriteLine();
    }

    private static void EmitForStatement(
        IndentedTextWriter indentedTextWriter, BoundForStatement statement, bool insideMain) {
        indentedTextWriter.Write("for (");
        EmitStatement(indentedTextWriter, statement.initializer, insideMain);
        indentedTextWriter.Indent++;
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);
        indentedTextWriter.Write("; ");
        EmitExpression(indentedTextWriter, statement.step);
        indentedTextWriter.Indent--;

        using (var forCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.body, insideMain);

        indentedTextWriter.WriteLine();
    }

    private static void EmitWhileStatement(
        IndentedTextWriter indentedTextWriter, BoundWhileStatement statement, bool insideMain) {
        indentedTextWriter.Write("while (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);

        using (var forCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.body, insideMain);

        indentedTextWriter.WriteLine();
    }

    private static void EmitDoWhileStatement(
        IndentedTextWriter indentedTextWriter, BoundDoWhileStatement statement, bool insideMain) {
        using (var forCurly = new CurlyIndenter(indentedTextWriter, "do"))
            EmitStatement(indentedTextWriter, statement.body, insideMain);

        indentedTextWriter.Write("while (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);
        indentedTextWriter.WriteLine(");");
        indentedTextWriter.WriteLine();
    }

    private static void EmitBreakStatement(IndentedTextWriter indentedTextWriter, BoundBreakStatement statement) {
        indentedTextWriter.WriteLine("break;");
    }

    private static void EmitContinueStatement(IndentedTextWriter indentedTextWriter, BoundContinueStatement statement) {
        indentedTextWriter.WriteLine("continue;");
    }

    private static void EmitExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
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
            case BoundNodeKind.ConstructorExpression:
                EmitConstructorExpression(indentedTextWriter, (BoundConstructorExpression)expression);
                break;
            case BoundNodeKind.MemberAccessExpression:
                EmitMemberAccessExpression(indentedTextWriter, (BoundMemberAccessExpression)expression);
                break;
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private static void EmitConstantExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        EmitBoundConstant(indentedTextWriter, expression.constantValue, expression.type);
    }

    private static void EmitBoundConstant(IndentedTextWriter indentedTextWriter, BoundConstant constant, BoundType type) {
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

    private static void EmitInitializerListExpression(
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

    private static void EmitUnaryExpression(IndentedTextWriter indentedTextWriter, BoundUnaryExpression expression) {
        indentedTextWriter.Write("(");
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write(")");
    }

    private static void EmitBinaryExpression(IndentedTextWriter indentedTextWriter, BoundBinaryExpression expression) {
        indentedTextWriter.Write("(");
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)} ");
        EmitExpression(indentedTextWriter, expression.right);
        indentedTextWriter.Write(")");
    }

    private static void EmitVariableExpression(
        IndentedTextWriter indentedTextWriter, BoundVariableExpression expression) {
        indentedTextWriter.Write(GetSafeName(expression.variable.name));
    }

    private static void EmitAssignmentExpression(
        IndentedTextWriter indentedTextWriter, BoundAssignmentExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write(" = ");
        EmitExpression(indentedTextWriter, expression.right);
    }

    private static void EmitEmptyExpression(IndentedTextWriter indentedTextWriter, BoundEmptyExpression expression) { }

    private static void EmitCallExpression(IndentedTextWriter indentedTextWriter, BoundCallExpression expression) {
        string functionName = null;

        switch (expression.function.name) {
            case "Print":
                functionName = "Console.Write";
                break;
            case "PrintLine":
                functionName = "Console.WriteLine";
                break;
            case "Input":
                functionName = "Console.ReadLine";
                break;
            case "RandInt":
                var signature = $"Func<{GetEquivalentType(expression.type)}>";
                indentedTextWriter.Write($"(({signature})(() => {{ var random = new System.Random(); return r.Next(");
                EmitExpression(indentedTextWriter, expression.arguments[0]);
                indentedTextWriter.Write("); }))()");
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
                    indentedTextWriter.Write("((");
                    EmitExpression(indentedTextWriter, expression.arguments[0]);
                    indentedTextWriter.Write($") is not null)");
                } else {
                    indentedTextWriter.Write("true");
                }

                return;
        }

        indentedTextWriter.Write($"{functionName ?? GetSafeName(expression.function.name)}(");

        var isFirst = true;

        foreach (var argument in expression.arguments) {
            if (isFirst)
                isFirst = false;
            else
                indentedTextWriter.Write(", ");

            EmitExpression(indentedTextWriter, argument);
        }

        indentedTextWriter.Write(")");
    }

    private static void EmitIndexExpression(IndentedTextWriter indentedTextWriter, BoundIndexExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write("[");
        EmitExpression(indentedTextWriter, expression.index);
        indentedTextWriter.Write("]");
    }

    private static void EmitCastExpression(IndentedTextWriter indentedTextWriter, BoundCastExpression expression) {
        indentedTextWriter.Write($"({GetEquivalentType(expression.type)})");
        EmitExpression(indentedTextWriter, expression.expression);
    }

    private static void EmitTernaryExpression(IndentedTextWriter indentedTextWriter, BoundTernaryExpression expression) {
        indentedTextWriter.Write("(");

        if (expression.left.type.isNullable)
            indentedTextWriter.Write("((");

        EmitExpression(indentedTextWriter, expression.left);

        if (expression.left.type.isNullable)
            indentedTextWriter.Write(") ?? throw new NullReferenceException())");

        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.leftOpKind)} ");
        EmitExpression(indentedTextWriter, expression.center);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.rightOpKind)} ");
        EmitExpression(indentedTextWriter, expression.right);
        indentedTextWriter.Write(")");
    }

    private static void EmitPrefixExpression(IndentedTextWriter indentedTextWriter, BoundPrefixExpression expression) {
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
    }

    private static void EmitPostfixExpression(IndentedTextWriter indentedTextWriter, BoundPostfixExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
    }

    private static void EmitCompoundAssignmentExpression(
        IndentedTextWriter indentedTextWriter, BoundCompoundAssignmentExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)}= ");
        EmitExpression(indentedTextWriter, expression.right);
    }

    private static void EmitReferenceExpression(IndentedTextWriter indentedTextWriter, BoundReferenceExpression expression) {
        indentedTextWriter.Write($"ref {GetSafeName(expression.variable.name)}");
    }

    private static void EmitConstructorExpression(
        IndentedTextWriter indentedTextWriter, BoundConstructorExpression expression) {
        indentedTextWriter.Write($"new {GetSafeName(expression.symbol.name)}()");
    }

    private static void EmitMemberAccessExpression(IndentedTextWriter indentedTextWriter, BoundMemberAccessExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write($".{GetSafeName(expression.member.name)}");
    }
}
