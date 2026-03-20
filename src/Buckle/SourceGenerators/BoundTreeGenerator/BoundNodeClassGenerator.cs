
#if NETSTANDARD

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BoundTreeGenerator;

/// <summary>
/// Generates all bound node classes for the compiler.
/// </summary>
[Generator]
public sealed partial class BoundNodeClassGenerator : IIncrementalGenerator {
    private static readonly DiagnosticDescriptor MissingBoundNodesXml = new DiagnosticDescriptor(
        "SG2001",
        title: "BoundNodes.xml is missing",
        messageFormat: "The BoundNodes.xml file was not included in the project, so we are not generating source",
        category: "BoundTreeGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnableToReadBoundNodesXml = new DiagnosticDescriptor(
        "SG2002",
        title: "BoundNodes.xml could not be read",
        messageFormat: "The BoundNodes.xml file could not even be read. Ensure it exists.",
        category: "BoundTreeGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BoundNodesXmlError = new DiagnosticDescriptor(
        "SG2003",
        title: "BoundNodes.xml has a syntax error",
        messageFormat: "{0}",
        category: "BoundTreeGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Initializes and invokes the generator, generating the class files.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var boundNodesXmlFiles = context.AdditionalTextsProvider
            .Where(at => Path.GetFileName(at.Path) == "BoundNodes.xml")
            .Collect();

        context.RegisterSourceOutput(boundNodesXmlFiles, static (context, boundNodesXmlFiles) => {
            var input = boundNodesXmlFiles.SingleOrDefault();

            if (input is null) {
                context.ReportDiagnostic(Diagnostic.Create(MissingBoundNodesXml, location: null));
                return;
            }

            var inputText = input.GetText();
            if (inputText is null) {
                context.ReportDiagnostic(Diagnostic.Create(UnableToReadBoundNodesXml, location: null));
                return;
            }

            Tree tree;

            try {
                var reader = XmlReader.Create(
                    new SourceTextReader(inputText),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }
                );

                var serializer = new XmlSerializer(typeof(Tree));
                tree = (Tree)serializer.Deserialize(reader);
            } catch (InvalidOperationException ex) when (ex.InnerException is XmlException exception) {
                var xmlException = exception;

                var line = inputText.Lines[xmlException.LineNumber - 1];
                var offset = xmlException.LinePosition - 1;
                var position = line.Start + offset;
                var span = new TextSpan(position, 0);
                var lineSpan = inputText.Lines.GetLinePositionSpan(span);

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        BoundNodesXmlError,
                        location: Location.Create(input.Path, span, lineSpan),
                        xmlException.Message));

                return;

            }

            DoGeneration(tree, context);
        });
    }

    private static void DoGeneration(Tree tree, SourceProductionContext context) {
        var sourcesBuilder = ImmutableArray.CreateBuilder<(string hintName, SourceText sourceText)>();
        AddResult(writer => BoundNodeClassWriter.Write(writer, tree), "BoundNodes.xml.Generated.cs");

        void AddResult(Action<TextWriter> writeFunction, string hintName) {
            var stringBuilder = new StringBuilder();
            using (var textWriter = new StringWriter(stringBuilder)) {
                writeFunction(textWriter);
            }

            var sourceText = SourceText.From(
                new StringBuilderReader(stringBuilder), stringBuilder.Length, encoding: Encoding.UTF8
            );

            context.AddSource(hintName, sourceText);
        }
    }
}

#endif
