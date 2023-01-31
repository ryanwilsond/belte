using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Generators;
using Diagnostics;
using MSSourceText = Microsoft.CodeAnalysis.Text.SourceText;

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
        MSSourceText sourceText;

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
                    EmitMainMethod(
                        indentedTextWriter, program.functionBodies.Single(p => p.Key == program.mainFunction)
                    );
                } else {
                    EmitEmptyMainMethod(indentedTextWriter);
                }

                foreach (var functionWithBody in program.functionBodies) {
                    if (functionWithBody.Key != program.mainFunction)
                        EmitMethod(indentedTextWriter, functionWithBody);
                }
            }

            indentedTextWriter.Flush();
            stringWriter.Flush();
            sourceText = MSSourceText.From(stringWriter.ToString(), Encoding.UTF8);
        }

        using (var writer = new StreamWriter(outputPath))
            sourceText.Write(writer);

        return program.diagnostics;
    }

    private static string GetEquivalentType(BoundType type) {
        return type.ToString();
    }

    private static string GetSafeName(string name) {
        return name;
    }

    private static void EmitStruct(
        IndentedTextWriter indentedTextWriter, KeyValuePair<StructSymbol, ImmutableList<FieldSymbol>> structure) {
        var signature = $"public struct {GetSafeName(structure.Key.name)}";

        using (var structCurly = new CurlyIndenter(indentedTextWriter, signature)) {

        }

        indentedTextWriter.WriteLine();
    }

    private static void EmitMainMethod(
        IndentedTextWriter indentedTextWriter, KeyValuePair<FunctionSymbol, BoundBlockStatement> method) {
        using (var methodCurly = new CurlyIndenter(indentedTextWriter, $"public static int Main()")) {
            EmitStatement(indentedTextWriter, method.Value);

            if (method.Key.type.typeSymbol == TypeSymbol.Void)
                indentedTextWriter.WriteLine("\nreturn 0;");
        }

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
            EmitStatement(indentedTextWriter, method.Value);

        indentedTextWriter.WriteLine();
    }

    private static void EmitStatement(IndentedTextWriter indentedTextWriter, BoundStatement statement) {

    }
}
