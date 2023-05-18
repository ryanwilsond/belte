using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Buckle.Generators;

using FieldList = List<(string name, string type, string kind, bool isOptional, bool isOverride)>;

/// <summary>
/// Represents a generator that has access to the language syntax.
/// </summary>
public abstract class SyntaxGenerator : IIncrementalGenerator {
    protected XmlElement syntax;
    protected List<string> knownTypes;
    protected const string indentString = "    ";

    /// <summary>
    /// Initializes generator.
    /// </summary>
    /// <param name="context">Generator context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var syntaxDocument = new XmlDocument();
        var syntaxResource = ReadResource("Buckle.Generators.Syntax.xml");
        syntaxDocument.LoadXml(syntaxResource);
        syntax = syntaxDocument.DocumentElement;

        knownTypes = new List<string>();
        var allTypes = syntax.ChildNodes;

        for (int i = 0; i < allTypes.Count; i++) {
            try {
                var typeName = allTypes.Item(i).Attributes["Name"]?.Value;
                Debug.Assert(typeName != null, "Name attribute is required");
                knownTypes.Add(typeName);
            } catch {
                // If this fails it means the node is a comment, and should be ignored
            }
        }

        var rootType = syntax.Attributes["Root"]?.Value;
        Debug.Assert(rootType != null, "Root attribute is required");
        knownTypes.Add(rootType);
    }

    /// <summary>
    /// Generates source.
    /// </summary>
    /// <param name="context">Generator context.</param>
    public virtual void Execute(GeneratorExecutionContext context) { }

    protected string ReadResource(string resourceName) {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
            throw new FileNotFoundException($"{nameof(resourceName)}: {resourceName}");

        using (var reader = new StreamReader(stream))
            return reader.ReadToEnd();
    }

    protected string GetGeneratorDirectory(GeneratorExecutionContext context) {
        var compilation = context.Compilation;

        // Using the SyntaxNode type as an anchor into the Buckle directory
        var nodeType = compilation.GetTypeByMetadataName("Buckle.CodeAnalysis.Syntax.SyntaxNode");
        var nodeFileName = nodeType.DeclaringSyntaxReferences.First().SyntaxTree.FilePath;
        var syntaxDirectory = Path.GetDirectoryName(nodeFileName);
        var generatedDirectory = Path.Combine(syntaxDirectory, "..", "Generated");

        Directory.CreateDirectory(generatedDirectory);
        return generatedDirectory;
    }

    protected void GenerateFromSourceText(GeneratorExecutionContext context, SourceText sourceText, string fileName) {
        var generatedDirectory = GetGeneratorDirectory(context);
        var filePath = Path.GetFullPath(Path.Combine(generatedDirectory, fileName));

        if (!File.Exists(filePath) || File.ReadAllText(filePath) != sourceText.ToString()) {
            if (!File.Exists(filePath))
                context.AddSource(fileName, sourceText);

            using (var writer = new StreamWriter(filePath))
                sourceText.Write(writer);
        }
    }

    protected FieldList DeserializeFields(XmlNode node) {
        var fields = new FieldList();

        for (int i = 1; i < node.ChildNodes.Count; i++) {
            var field = node.ChildNodes.Item(i);

            try {
                var name = field.Attributes["Name"]?.Value;
                Debug.Assert(name != null, "Name attribute is required");
                var type = field.Attributes["Type"]?.Value;
                Debug.Assert(type != null, "Type attribute is required");
                var optional = field.Attributes["Optional"]?.Value;
                var @override = field.Attributes["Override"]?.Value;

                var hasKind = field.HasChildNodes;
                var kind = hasKind ? field.FirstChild.Attributes["Name"]?.Value : null;

                if (hasKind) {
                    Debug.Assert(field.FirstChild.Name == "Kind", "first field child must be a Kind xml node");
                    Debug.Assert(kind != null, "Name is a required attribute");
                }

                fields.Add((
                    name,
                    type,
                    kind,
                    optional == null ? false : optional.ToLower() == "true",
                    @override == null ? false : @override.ToLower() == "true"
                ));
            } catch {
                // If this fails, it means the node is a comment and should be ignored
            }
        }

        return fields;
    }

    protected string ShortName(string typeName) {
        return typeName.Replace("Syntax", "");
    }
}
