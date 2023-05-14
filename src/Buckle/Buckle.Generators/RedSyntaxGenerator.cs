using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Buckle.Generators.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Buckle.Generators;

using FieldList = List<(string name, string type, bool isOptional, bool isOverride)>;

/// <summary>
/// Generates all of the red syntax.
/// </summary>
[Generator]
public sealed class RedSyntaxGenerator : SyntaxGenerator {
    public override void Execute(GeneratorExecutionContext context) {
        var compilation = (CSharpCompilation)context.Compilation;

        SourceText sourceText;

        using (var stringWriter = new StringWriter())
        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
            indentedTextWriter.WriteLine("using Buckle.CodeAnalysis.Syntax.InternalSyntax;");
            indentedTextWriter.WriteLine();
            indentedTextWriter.WriteLine("namespace Buckle.CodeAnalysis.Syntax;");
            indentedTextWriter.WriteLine();

            GenerateAbstractNodes(indentedTextWriter);
            GenerateNodes(indentedTextWriter);

            indentedTextWriter.Flush();
            stringWriter.Flush();
            sourceText = SourceText.From(stringWriter.ToString(), Encoding.UTF8);
        }

        GenerateFromSourceText(context, sourceText, "Syntax.xml.Syntax.g.cs");
    }

    private void GenerateAbstractNodes(IndentedTextWriter writer) {
        var abstractNodes = syntax.GetElementsByTagName("AbstractNode");

        for (int i = 0; i < abstractNodes.Count; i++) {
            var abstractNode = abstractNodes.Item(i);

            var typeName = abstractNode.Attributes["Name"]?.Value;
            Debug.Assert(typeName != null);
            var baseName = abstractNode.Attributes["Base"]?.Value;
            Debug.Assert(baseName != null);
            Debug.Assert(knownTypes.Contains(baseName));

            using (var classCurly = new CurlyIndenter(writer, $"public abstract class {typeName} : {baseName}")) {
                writer.WriteLine($"internal {typeName}(SyntaxNode parent, GreenNode green, int position)");
                writer.WriteLine($"{indentString}: base(parent, green, position) {{ }}");
                writer.WriteLine();

                var fields = DeserializeFields(abstractNode);
                GenerateFieldDeclarations(writer, fields, typeName, isAbstract: true);
            }

            writer.WriteLine();
        }
    }

    private void GenerateNodes(IndentedTextWriter writer) {
        var nodes = syntax.GetElementsByTagName("Node");

        for (int i = 0; i < nodes.Count; i++) {
            var node = nodes.Item(i);

            var typeName = node.Attributes["Name"]?.Value;
            Debug.Assert(typeName != null);
            var baseName = node.Attributes["Base"]?.Value;
            Debug.Assert(baseName != null);
            Debug.Assert(knownTypes.Contains(baseName));

            var fields = DeserializeFields(node);

            using (var classCurly = new CurlyIndenter(writer, $"public sealed class {typeName} : {baseName}")) {
                GeneratePrivateFieldDeclarations(writer, fields);

                writer.WriteLine();
                writer.WriteLine($"internal {typeName}(SyntaxNode parent, GreenNode green, int position)");
                writer.WriteLine($"{indentString}: base(parent, green, position) {{ }}");
                writer.WriteLine();

                GenerateFieldDeclarations(writer, fields, typeName);
                GenerateGetNodeSlotMethod(writer, fields);
                writer.WriteLine();
                GenerateGetCachedSlotMethod(writer, fields);
            }

            writer.WriteLine();

            using (var classCurly = new CurlyIndenter(writer, $"internal static partial class SyntaxFactory"))
                GenerateFactoryMethod(writer, fields, typeName);

            writer.WriteLine();
        }
    }

    private void GeneratePrivateFieldDeclarations(IndentedTextWriter writer, FieldList fields) {
        foreach (var field in fields) {
            if (field.type == "SyntaxToken")
                continue;

            if (knownTypes.Contains(field.type))
                writer.WriteLine($"private {field.type} _{field.name};");
            else
                writer.WriteLine($"private SyntaxNode _{field.name};");
        }
    }

    private void GenerateFieldDeclarations(IndentedTextWriter writer, FieldList fields, string typeName, bool isAbstract = false) {
        for (int i = 0; i < fields.Count; i++) {
            var field = fields[i];
            Debug.Assert(!(field.isOverride & isAbstract));

            if (isAbstract) {
                writer.WriteLine($"public abstract {field.type} {field.name} {{ get; }}");
                writer.WriteLine();
                continue;
            }

            var modifiers = "public";

            if (field.isOverride)
                modifiers += " override";

            if (field.type == "SyntaxToken") {
                writer.WriteLine(
                    $"{modifiers} {field.type} {field.name} => new SyntaxToken(this, ((Syntax.InternalSyntax." +
                    $"{typeName})green).{field.name}, GetChildPosition({i}), GetChildIndex({i}));"
                );
            } else if (field.type.StartsWith("SeparatedSyntaxList")) {
                writer.WriteLine(
                    $"{modifiers} {field.type} {field.name} => new {field.type}" +
                    $"(GetRed(ref this._{field.name}, {i}), GetChildIndex({i}));"
                );
            } else if (!knownTypes.Contains(field.type)) {
                writer.WriteLine(
                    $"{modifiers} {field.type} {field.name} => new {field.type}(GetRed(ref this._{field.name}, {i}));"
                );
            } else {
                writer.WriteLine($"{modifiers} {field.type} {field.name} => GetRed(ref this._{field.name}, {i});");
            }

            writer.WriteLine();
        }
    }

    private void GenerateGetNodeSlotMethod(IndentedTextWriter writer, FieldList fields) {
        var declaration = $"internal override SyntaxNode GetNodeSlot(int index) => index switch";
        var index = 0;

        using (var methodCurly = new CurlyIndenter(writer, declaration, includeSemicolon: true)) {
            for (int i = 0; i < fields.Count; i++) {
                var field = fields[i];

                if (field.type == "SyntaxToken")
                    continue;

                if (index == 0)
                    writer.WriteLine($"0 => GetRedAtZero(ref this._{field.name}),");
                else
                    writer.WriteLine($"{index} => GetRed(ref this._{field.name}, {index}),");

                index++;
            }

            writer.WriteLine("_ => null,");
        }
    }

    private void GenerateGetCachedSlotMethod(IndentedTextWriter writer, FieldList fields) {
        var declaration = $"internal override SyntaxNode GetCachedSlot(int index) => index switch";
        var index = 0;

        using (var methodCurly = new CurlyIndenter(writer, declaration, includeSemicolon: true)) {
            for (int i = 0; i < fields.Count; i++) {
                var field = fields[i];

                if (field.type == "SyntaxToken")
                    continue;

                writer.WriteLine($"{index} => this._{field.name},");
                index++;
            }

            writer.WriteLine("_ => null,");
        }
    }

    private void GenerateFactoryMethod(IndentedTextWriter writer, FieldList fields, string typeName) {
        var allArguments = "";
        var requiredArguments = "";
        var allParameters = "";
        var requiredParameters = "";

        string GenerateParameter(string name, string type) {
            if (type.StartsWith("SyntaxList"))
                return $"{name}.node.ToGreenList<Syntax.InternalSyntax.{type.Substring(11, type.Length - 12)}>()";
            else if (type.StartsWith("SeparatedSyntaxList"))
                return $"{name}.node.ToGreenSeparatedList<Syntax.InternalSyntax.{type.Substring(20, type.Length - 21)}>()";
            else if (!knownTypes.Contains(type) || type == "SyntaxToken")
                return $"(Syntax.InternalSyntax.{type}){name}.node";
            else
                return $"(Syntax.InternalSyntax.{type}){name}.green";
        }

        string GenerateArgument(string name, string type) {
            return $"{type} {name}";
        }

        for (int i = 0; i < fields.Count; i++) {
            var field = fields[i];
            allParameters += GenerateParameter(field.name, field.type);
            allArguments += GenerateArgument(field.name, field.type);

            if (!field.isOptional) {
                requiredParameters += GenerateParameter(field.name, field.type);
                requiredArguments += GenerateArgument(field.name, field.type);
            } else {
                requiredParameters += "null";
            }

            if (i < fields.Count - 1) {
                allParameters += ", ";
                allArguments += ", ";
                requiredParameters += ", ";

                if (!field.isOptional)
                    requiredArguments += ", ";
            }
        }

        if (requiredArguments.EndsWith(", "))
            requiredArguments = requiredArguments.Substring(0, requiredArguments.Length - 2);

        writer.WriteLine($"internal static {typeName} {ShortName(typeName)}({allArguments})");
        writer.WriteLine(
            $"{indentString}=> ({typeName})Syntax.InternalSyntax.SyntaxFactory." +
            $"{ShortName(typeName)}({allParameters}).CreateRed();"
        );

        if (allArguments != requiredArguments) {
            writer.WriteLine();
            writer.WriteLine($"internal static {typeName} {ShortName(typeName)}({requiredArguments})");
            writer.WriteLine(
                $"{indentString}=> ({typeName})Syntax.InternalSyntax.SyntaxFactory." +
                $"{ShortName(typeName)}({requiredParameters}).CreateRed();"
            );
        }
    }
}
