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
            indentedTextWriter.WriteLine("using Diagnostics;");
            indentedTextWriter.WriteLine();
            indentedTextWriter.WriteLine("namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;");
            indentedTextWriter.WriteLine();

            GenerateAbstractNodes(indentedTextWriter);
            GenerateNodes(indentedTextWriter);
            GenerateSyntaxVisitorT(indentedTextWriter);
            GenerateSyntaxVisitor(indentedTextWriter);
            GenerateSyntaxRewriter(indentedTextWriter);

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

            baseName = baseName == "SyntaxNode" ? "BelteSyntaxNode" : baseName;

            using (var classCurly =
                new CurlyIndenter(writer, $"internal abstract class {typeName} : {baseName}")) {
                writer.WriteLine($"internal {typeName}(SyntaxKind kind)");
                writer.WriteLine($"{indentString}: base(kind) {{ }}");
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
            var fields = DeserializeFields(node);

            using (var classCurly = new CurlyIndenter(writer, $"internal sealed class {typeName} : {baseName}")) {
                GeneratePrivateFieldDeclarations(writer, fields);
                writer.WriteLine();

                GenerateConstructor(writer, node, fields, typeName);
                using (var constructorCurly = new CurlyIndenter(writer)) {
                    writer.WriteLine($"slotCount = {fields.Count};");
                    GenerateConstructorBody(writer, fields);
                }

                writer.WriteLine();

                GenerateConstructorWithDiagnostics(writer, node, fields, typeName);
                using (var constructorCurly = new CurlyIndenter(writer)) {
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
                writer.WriteLine();

                writer.WriteLine(
                    $"internal override void Accept(SyntaxVisitor visitor) => visitor.Visit{typeName}(this);"
                );
                writer.WriteLine();

                writer.WriteLine(
                    $"internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => " +
                    $"visitor.Visit{typeName}(this);"
                );
                writer.WriteLine();

                GenerateUpdateMethod(writer, fields, typeName);
                writer.WriteLine();
                GenerateSetDiagnosticsMethod(writer, fields, typeName);
            }

            writer.WriteLine();

            using (var classCurly = new CurlyIndenter(writer, $"internal static partial class SyntaxFactory"))
                GenerateFactoryMethod(writer, fields, typeName);
        }
    }

    private void GenerateConstructor(IndentedTextWriter writer, XmlNode node, FieldList fields, string typeName) {
        var kind = node.FirstChild;
        Debug.Assert(kind.Name == "Kind");
        var syntaxKind = kind.Attributes["Name"]?.Value;
        Debug.Assert(syntaxKind != null);

        var arguments = GenerateArguments(fields);

        writer.WriteLine($"internal {typeName}({arguments})");
        writer.WriteLine($"{indentString}: base(SyntaxKind.{syntaxKind})");
    }

    private void GenerateConstructorWithDiagnostics(
        IndentedTextWriter writer, XmlNode node, FieldList fields, string typeName) {
        var kind = node.FirstChild;
        Debug.Assert(kind.Name == "Kind");
        var syntaxKind = kind.Attributes["Name"]?.Value;
        Debug.Assert(syntaxKind != null);

        var arguments = GenerateArguments(fields);

        writer.WriteLine($"internal {typeName}({arguments}, Diagnostic[] diagnostics)");
        writer.WriteLine($"{indentString}: base(SyntaxKind.{syntaxKind}, diagnostics)");
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

    private void GenerateUpdateMethod(IndentedTextWriter writer, FieldList fields, string typeName) {
        var arguments = "";
        var condition = "";
        var parameters = "";

        for (int i = 0; i < fields.Count; i++) {
            var field = fields[i];
            arguments += $"{field.type} {field.name}";
            condition += $"{field.name} != this.{field.name}";
            parameters += field.name;

            if (i < fields.Count - 1) {
                arguments += ", ";
                condition += " || ";
                parameters += ", ";
            }
        }

        using (var methodCurly = new CurlyIndenter(writer, $"internal {typeName} Update({arguments})")) {
            using (var ifCurly = new CurlyIndenter(writer, $"if ({condition})")) {
                writer.WriteLine($"var newNode = SyntaxFactory.{ShortName(typeName)}({parameters});");
                writer.WriteLine("var diagnostics = GetDiagnostics();");
                writer.WriteLine("if (diagnostics.Length > 0)");
                writer.WriteLine($"{indentString}newNode = newNode.WithDiagnosticsGreen(diagnostics);");
                writer.WriteLine("return newNode;");
            }

            writer.WriteLine();
            writer.WriteLine("return this;");
        }
    }

    private void GenerateSetDiagnosticsMethod(IndentedTextWriter writer, FieldList fields, string typeName) {
        var parameters = "";

        foreach (var field in fields)
            parameters += $"{field.name}, ";

        writer.WriteLine("internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics)");
        writer.WriteLine($"{indentString}=> new {typeName}({parameters}diagnostics);");
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

        writer.WriteLine($"internal static {typeName} {ShortName(typeName)}({allArguments})");
        writer.WriteLine($"{indentString}=> new {typeName}({allParameters});");

        if (allArguments != requiredArguments) {
            writer.WriteLine($"internal static {typeName} {ShortName(typeName)}({requiredArguments})");
            writer.WriteLine($"{indentString}=> new {typeName}({requiredParameters});");
        }
    }

    private void GenerateSyntaxVisitorT(IndentedTextWriter writer) {
        var nodes = syntax.GetElementsByTagName("Node");

        using (var classCurly = new CurlyIndenter(writer, $"internal partial class SyntaxVisitor<TResult>")) {
            for (int i = 0; i < nodes.Count; i++) {
                var node = nodes.Item(i);

                var typeName = node.Attributes["Name"]?.Value;
                Debug.Assert(typeName != null);

                writer.WriteLine(
                    $"internal virtual TResult Visit{ShortName(typeName)}({typeName} node) => DefaultVisit(node);"
                );
            }
        }
    }

    private void GenerateSyntaxVisitor(IndentedTextWriter writer) {
        var nodes = syntax.GetElementsByTagName("Node");

        using (var classCurly = new CurlyIndenter(writer, $"internal partial class SyntaxVisitor")) {
            for (int i = 0; i < nodes.Count; i++) {
                var node = nodes.Item(i);

                var typeName = node.Attributes["Name"]?.Value;
                Debug.Assert(typeName != null);

                writer.WriteLine(
                    $"internal virtual void Visit{ShortName(typeName)}({typeName} node) => DefaultVisit(node);"
                );
            }
        }
    }

    private void GenerateSyntaxRewriter(IndentedTextWriter writer) {
        var nodes = syntax.GetElementsByTagName("Node");

        var declaration = "internal partial class SyntaxRewriter : SyntaxVisitor<BelteSyntaxNode>";
        using (var classCurly = new CurlyIndenter(writer, declaration)) {
            for (int i = 0; i < nodes.Count; i++) {
                var node = nodes.Item(i);

                var typeName = node.Attributes["Name"]?.Value;
                Debug.Assert(typeName != null);

                writer.WriteLine($"internal override BelteSyntaxNode Visit{ShortName(typeName)}({typeName} node)");
                GenerateVisitBody(writer, node);

                writer.WriteLine();
            }
        }
    }

    private void GenerateVisitBody(IndentedTextWriter writer, XmlNode node) {
        var fields = DeserializeFields(node);

        var parameters = "";

        for (int i = 0; i < fields.Count; i++) {
            var field = fields[i];

            if (field.type.StartsWith("SyntaxList") || field.type.StartsWith("SeparatedSyntaxList"))
                parameters += $"VisitList(node.{field.name})";
            else
                parameters += $"({field.type})Visit(node.{field.name})";

            if (i < fields.Count - 1)
                parameters += ", ";
        }

        writer.WriteLine($"{indentString}=> node.Update({parameters});");
    }
}
