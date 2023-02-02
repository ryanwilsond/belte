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

        string indentString = "    ";

        using (var stringWriter = new StringWriter())
        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
            indentedTextWriter.WriteLine("using System;");
            indentedTextWriter.WriteLine("using System.Collections.Generic;");
            indentedTextWriter.WriteLine($"\nnamespace {GetSafeName(Path.GetFileNameWithoutExtension(outputPath))};\n");

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
            stringWriter.Flush();

            using (var writer = new StreamWriter(outputPath))
                writer.Write(stringWriter);
        }

        return program.diagnostics;
    }

    private static string GetEquivalentType(BoundType type) {
        if (type.typeSymbol == TypeSymbol.Void)
            return "void";

        var equivalentType = new StringBuilder();
        var typeName = type.typeSymbol is StructSymbol ? GetSafeName(type.typeSymbol.name) : type.typeSymbol.name;

        // All logic relating to constants has already been handled by the Binder, so specifying const here would
        // not do anything
        if (type.isReference)
            equivalentType.Append("ref ");
        if (type.isNullable)
            typeName = $"Nullable<{typeName}>";

        for (int i=0; i<type.dimensions; i++)
            typeName = $"List<{typeName}>";

        equivalentType.Append(typeName);

        return equivalentType.ToString();
    }

    private static string GetSafeName(string name) {
        CodeDomProvider provider = CodeDomProvider.CreateProvider("C#");
        return provider.IsValidIdentifier(name) ? name : "@" + name;
    }

    private static void EmitStruct(
        IndentedTextWriter indentedTextWriter, KeyValuePair<StructSymbol, ImmutableList<FieldSymbol>> structure) {
        var signature = $"public struct {GetSafeName(structure.Key.name)}";

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
        var signature = $"public static {GetEquivalentType(method.Key.type)} Main()";

        using (var methodCurly = new CurlyIndenter(indentedTextWriter, signature))
            EmitBody(indentedTextWriter, method.Value);

        indentedTextWriter.WriteLine();
    }

    private static void EmitEmptyMainMethod(IndentedTextWriter indentedTextWriter) {
        using (var methodCurly = new CurlyIndenter(indentedTextWriter, $"public static void Main()"))

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

    private static void EmitBody(IndentedTextWriter indentedTextWriter, BoundBlockStatement body) {
        foreach (var statement in body.statements)
            EmitStatement(indentedTextWriter, statement);
    }

    private static void EmitStatement(IndentedTextWriter indentedTextWriter, BoundStatement statement) {
        switch (statement.kind) {
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
        EmitExpression(indentedTextWriter, statement.expression);
        indentedTextWriter.WriteLine(";");
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
        indentedTextWriter.Write($"if (");

        if (statement.jumpIfTrue)
            indentedTextWriter.Write("(");
        else
            indentedTextWriter.Write("!(");

        EmitExpression(indentedTextWriter, statement.condition);

        indentedTextWriter.WriteLine("))");
        indentedTextWriter.Indent++;
        indentedTextWriter.WriteLine($"goto {statement.label.name};");
        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine();
    }

    private static void EmitReturnStatement(IndentedTextWriter indentedTextWriter, BoundReturnStatement statement) {
        if (statement.expression == null) {
            indentedTextWriter.WriteLine("return;");
        } else {
            indentedTextWriter.Write("return ");
            EmitExpression(indentedTextWriter, statement.expression);
            indentedTextWriter.WriteLine(";");
        }
    }

    private static void EmitTryStatement(IndentedTextWriter indentedTextWriter, BoundTryStatement statement) {
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
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private static void EmitConstantExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        if (expression is BoundInitializerListExpression il) {
            EmitInitializerListExpression(indentedTextWriter, il);
        } else {
            if (BoundConstant.IsNull(expression.constantValue))
                indentedTextWriter.Write("null");
            else
                indentedTextWriter.Write(expression.constantValue.value);
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
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
    }

    private static void EmitBinaryExpression(IndentedTextWriter indentedTextWriter, BoundBinaryExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)} ");
        EmitExpression(indentedTextWriter, expression.right);
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

    private static void EmitEmptyExpression(IndentedTextWriter indentedTextWriter, BoundEmptyExpression expression) {
        indentedTextWriter.Write("/* EmptyExpression */");
    }

    private static void EmitCallExpression(IndentedTextWriter indentedTextWriter, BoundCallExpression expression) {
        indentedTextWriter.Write($"{GetSafeName(expression.function.name)}(");

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
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.leftOpKind)} ");
        EmitExpression(indentedTextWriter, expression.center);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.rightOpKind)} ");
        EmitExpression(indentedTextWriter, expression.right);
    }
}
