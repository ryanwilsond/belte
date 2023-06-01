
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

namespace SyntaxGenerator;

/// <summary>
/// Generates all red and green syntax files for the compiler.
/// </summary>
[Generator]
public sealed class SourceGenerator : IIncrementalGenerator {
    private static readonly DiagnosticDescriptor s_MissingSyntaxXml = new DiagnosticDescriptor(
        "SG1001",
        title: "Syntax.xml is missing",
        messageFormat: "The Syntax.xml file was not included in the project, so we are not generating source",
        category: "SyntaxGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_UnableToReadSyntaxXml = new DiagnosticDescriptor(
        "SG1002",
        title: "Syntax.xml could not be read",
        messageFormat: "The Syntax.xml file could not even be read. Ensure it exists.",
        category: "SyntaxGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_SyntaxXmlError = new DiagnosticDescriptor(
        "SG1003",
        title: "Syntax.xml has a syntax error",
        messageFormat: "{0}",
        category: "SyntaxGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Initializes and invokes the generator, generating the syntax files.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var syntaxXmlFiles = context.AdditionalTextsProvider.Where(at => Path.GetFileName(at.Path) == "Syntax.xml").Collect();

        context.RegisterSourceOutput(syntaxXmlFiles, static (context, syntaxXmlFiles) => {
            var input = syntaxXmlFiles.SingleOrDefault();

            if (input is null) {
                context.ReportDiagnostic(Diagnostic.Create(s_MissingSyntaxXml, location: null));
                return;
            }

            var inputText = input.GetText();
            if (inputText is null) {
                context.ReportDiagnostic(Diagnostic.Create(s_UnableToReadSyntaxXml, location: null));
                return;
            }

            Tree tree;

            try {
                var reader = XmlReader.Create(new SourceTextReader(inputText), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
                var serializer = new XmlSerializer(typeof(Tree));
                tree = (Tree)serializer.Deserialize(reader);
            } catch (InvalidOperationException ex) when (ex.InnerException is XmlException) {
                var xmlException = (XmlException)ex.InnerException;

                var line = inputText.Lines[xmlException.LineNumber - 1];
                int offset = xmlException.LinePosition - 1;
                var position = line.Start + offset;
                var span = new TextSpan(position, 0);
                var lineSpan = inputText.Lines.GetLinePositionSpan(span);

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        s_SyntaxXmlError,
                        location: Location.Create(input.Path, span, lineSpan),
                        xmlException.Message));

                return;

            }

            DoGeneration(tree, context);
        });
    }

    private static void DoGeneration(Tree tree, SourceProductionContext context) {
        TreeFlattening.FlattenChildren(tree);

        var sourcesBuilder = ImmutableArray.CreateBuilder<(string hintName, SourceText sourceText)>();
        addResult(writer => SourceWriter.WriteInternal(writer, tree), "Syntax.xml.Internal.Generated.cs");
        addResult(writer => SourceWriter.WriteSyntax(writer, tree), "Syntax.xml.Syntax.Generated.cs");

        void addResult(Action<TextWriter> writeFunction, string hintName) {
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

    private sealed class SourceTextReader : TextReader {
        private readonly SourceText _sourceText;
        private int _position;

        public SourceTextReader(SourceText sourceText) {
            _sourceText = sourceText;
            _position = 0;
        }

        public override int Peek() {
            if (_position == _sourceText.Length) {
                return -1;
            }

            return _sourceText[_position];
        }

        public override int Read() {
            if (_position == _sourceText.Length) {
                return -1;
            }

            return _sourceText[_position++];
        }

        public override int Read(char[] buffer, int index, int count) {
            var charsToCopy = Math.Min(count, _sourceText.Length - _position);
            _sourceText.CopyTo(_position, buffer, index, charsToCopy);
            _position += charsToCopy;
            return charsToCopy;
        }
    }

    private sealed class StringBuilderReader : TextReader {
        private readonly StringBuilder _stringBuilder;
        private int _position;

        public StringBuilderReader(StringBuilder stringBuilder) {
            _stringBuilder = stringBuilder;
            _position = 0;
        }

        public override int Peek() {
            if (_position == _stringBuilder.Length) {
                return -1;
            }

            return _stringBuilder[_position];
        }

        public override int Read() {
            if (_position == _stringBuilder.Length) {
                return -1;
            }

            return _stringBuilder[_position++];
        }

        public override int Read(char[] buffer, int index, int count) {
            var charsToCopy = Math.Min(count, _stringBuilder.Length - _position);
            _stringBuilder.CopyTo(_position, buffer, index, charsToCopy);
            _position += charsToCopy;
            return charsToCopy;
        }
    }
}

#endif
