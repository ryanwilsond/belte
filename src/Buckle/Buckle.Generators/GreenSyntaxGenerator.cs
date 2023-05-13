using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Buckle.Generators;

using FieldList = List<(string name, string type, bool isOptional, bool isOverride)>;

/// <summary>
/// Generates all of the green syntax.
/// </summary>
[Generator]
public sealed class GreenSyntaxGenerator : SyntaxGenerator {
    public override void Execute(GeneratorExecutionContext context) {
        var compilation = (CSharpCompilation)context.Compilation;

        SourceText sourceText;

        using (var stringWriter = new StringWriter())
        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
            indentedTextWriter.WriteLine();
            indentedTextWriter.WriteLine("namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;");
            indentedTextWriter.WriteLine();

            GenerateAbstractNodes(indentedTextWriter);
            GenerateNodes(indentedTextWriter);

            indentedTextWriter.Flush();
            stringWriter.Flush();
            sourceText = SourceText.From(stringWriter.ToString(), Encoding.UTF8);
        }

        GenerateFromSourceText(context, sourceText, "Syntax.xml.Internal.g.cs");
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

            baseName = baseName == "SyntaxNode" ? "GreenNode" : baseName;

            using (var classCurly =
                new CurlyIndenter(writer, $"internal abstract class {typeName} : {baseName}")) {
                var constructor = $"internal {typeName}(SyntaxKind kind) : base(kind) {{ }}";
                writer.WriteLine(constructor);
                writer.WriteLine();

                var fields = DeserializeFields(abstractNode);
                GenerateFieldDeclarations(writer, fields, isAbstract: true);
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

            baseName = baseName == "SyntaxNode" ? "GreenNode" : baseName;
            FieldList fields;

            using (var classCurly = new CurlyIndenter(writer, $"internal sealed class {typeName} : {baseName}")) {
                (var constructor, fields) = GenerateConstructorAndFields(node, typeName);
                GeneratePrivateFieldDeclarations(writer, fields);
                writer.WriteLine();

                using (var constructorCurly = new CurlyIndenter(writer, constructor)) {
                    writer.WriteLine($"slotCount = {fields.Count};");
                    GenerateConstructorBody(writer, fields);
                }

                writer.WriteLine();

                GenerateFieldDeclarations(writer, fields);
                GenerateGetSlotMethod(writer, fields);

                writer.WriteLine();
                var createRedDeclaration = "internal override SyntaxNode CreateRed(SyntaxNode parent, int position)";
                var createRedBody = $"=> new Syntax.{typeName}(parent, this, position);";
                writer.WriteLine($"{createRedDeclaration} {createRedBody}");
            }

            writer.WriteLine();

            using (var classCurly = new CurlyIndenter(writer, $"internal static partial class SyntaxFactory"))
                GenerateFactoryMethod(writer, fields, typeName);

            writer.WriteLine();
        }
    }

    private (string, FieldList) GenerateConstructorAndFields(
        XmlNode node, string typeName) {
        var kind = node.FirstChild;
        Debug.Assert(kind.Name == "Kind");
        var syntaxKind = kind.Attributes["Name"]?.Value;
        Debug.Assert(syntaxKind != null);

        var fields = DeserializeFields(node);
        var arguments = GenerateArguments(fields);
        var constructor = $"internal {typeName}({arguments}) : base(SyntaxKind.{syntaxKind})";

        return (constructor, fields);
    }

    private string GenerateArguments(FieldList fields) {
        var arguments = "";

        for (int i = 0; i < fields.Count; i++) {
            var field = fields[i];

            if (knownTypes.Contains(field.type))
                arguments += $"{field.type} {field.name}";
            else
                arguments += $"GreenNode {field.name}";

            if (i < fields.Count - 1)
                arguments += ", ";
        }

        return arguments;
    }

    private void GeneratePrivateFieldDeclarations(IndentedTextWriter writer, FieldList fields) {
        foreach (var field in fields) {
            if (knownTypes.Contains(field.type))
                writer.WriteLine($"internal readonly {field.type} _{field.name};");
            else
                writer.WriteLine($"internal readonly GreenNode _{field.name};");
        }
    }

    private void GenerateConstructorBody(IndentedTextWriter writer, FieldList fields) {
        void GenerateFieldAssignment(string fieldName) {
            writer.WriteLine($"AdjustFlagsAndWidth({fieldName});");
            writer.WriteLine($"this._{fieldName} = {fieldName};");
        }

        foreach (var field in fields) {
            if (field.isOptional) {
                using (var ifCurly = new CurlyIndenter(writer, $"if ({field.name} != null)"))
                    GenerateFieldAssignment(field.name);
            } else {
                GenerateFieldAssignment(field.name);
            }
        }
    }

    private void GenerateFieldDeclarations(IndentedTextWriter writer, FieldList fields, bool isAbstract = false) {
        foreach (var field in fields) {
            Debug.Assert(!(field.isOverride & isAbstract));
            var fieldType = field.type;
            var modifiers = "internal";

            if (!knownTypes.Contains(field.type))
                fieldType = $"InternalSyntax.{fieldType}";

            if (field.isOverride)
                modifiers += " override";

            if (isAbstract)
                writer.WriteLine($"internal abstract {fieldType} {field.name} {{ get; }}");
            else if (knownTypes.Contains(field.type))
                writer.WriteLine($"{modifiers} {fieldType} {field.name} => this._{field.name};");
            else
                writer.WriteLine($"{modifiers} {fieldType} {field.name} => new {fieldType}(this._{field.name});");

            writer.WriteLine();
        }
    }

    private void GenerateGetSlotMethod(IndentedTextWriter writer, FieldList fields) {
        var declaration = $"internal override GreenNode GetSlot(int index) => index switch";

        using (var methodCurly = new CurlyIndenter(writer, declaration, includeSemicolon: true)) {
            for (int i = 0; i < fields.Count; i++)
                writer.WriteLine($"{i} => this._{fields[i].name},");

            writer.WriteLine("_ => null,");
        }
    }

    private void GenerateFactoryMethod(IndentedTextWriter writer, FieldList fields, string typeName) {
        var allArguments = "";
        var requiredArguments = "";
        var allParameters = "";
        var requiredParameters = "";

        for (int i = 0; i < fields.Count; i++) {
            var field = fields[i];
            allParameters += field.name;
            allArguments += $"{field.type} {field.name}";

            if (!field.isOptional) {
                requiredParameters += field.name;
                requiredArguments += $"{field.type} {field.name}";
            } else {
                requiredParameters += "null";
            }

            if (!knownTypes.Contains(field.type)) {
                allParameters += ".node";
                requiredParameters += ".node";
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

        writer.WriteLine(
            $"internal static {typeName} {typeName.Replace("Syntax", "")}({allArguments}) " +
            $"=> new {typeName}({allParameters});"
        );

        if (allArguments != requiredArguments) {
            writer.WriteLine(
                $"internal static {typeName} {typeName.Replace("Syntax", "")}({requiredArguments}) " +
                $"=> new {typeName}({requiredParameters});"
            );
        }
    }
}
