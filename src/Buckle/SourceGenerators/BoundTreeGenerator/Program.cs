
#if NETCOREAPP

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace BoundTreeGenerator;

public static class Program {
    public static int Main(string[] args) {
        if (args.Length != 2)
            return WriteUsage();

        var inputFile = args[0];

        if (!File.Exists(inputFile)) {
            Console.WriteLine(inputFile + " not found.");
            return 1;
        }

        var outputFile = args[1];

        return WriteSourceFiles(inputFile, outputFile);
    }

    private static int WriteUsage() {
        Console.WriteLine("Invalid usage:");
        var programName = "  " + typeof(Program).GetTypeInfo().Assembly.ManifestModule.Name;
        Console.WriteLine(programName + " input-file output-file");

        return 1;
    }

    private static Tree ReadTree(string inputFile) {
        var reader = XmlReader.Create(inputFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var serializer = new XmlSerializer(typeof(Tree));

        return (Tree)serializer.Deserialize(reader);
    }

    private static int WriteSourceFiles(string inputFile, string outputFile) {
        var tree = ReadTree(inputFile);

        var outputPath = outputFile.Trim('"');
        var prefix = Path.GetFileName(inputFile);
        var outFile = Path.Combine(outputPath, $"{prefix}.Generated.cs");

        WriteToFile(writer => BoundNodeClassWriter.Write(writer, tree), outFile);

        return 0;
    }

    private static void WriteToFile(Action<TextWriter> writeAction, string outputFile) {
        var stringBuilder = new StringBuilder();
        var writer = new StringWriter(stringBuilder);
        writeAction(writer);

        var text = stringBuilder.ToString();
        int length;

        do {
            length = text.Length;
            text = text.Replace($"{{{Environment.NewLine}{Environment.NewLine}", $"{{{Environment.NewLine}");
        } while (text.Length != length);

        try {
            using var outFile = new StreamWriter(File.Open(outputFile, FileMode.Create), Encoding.UTF8);
            outFile.Write(text);
        } catch (UnauthorizedAccessException) {
            Console.WriteLine("Unable to access {0}. Is it checked out?", outputFile);
        }
    }
}

#endif
