using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Buckle.Generators {
    [Generator]
    public class GetChildrenGenerator : ISourceGenerator {
        public void Initialize(GeneratorInitializationContext context) {
        }

        public void Execute(GeneratorExecutionContext context) {
            var compilation = (CSharpCompilation)context.Compilation;

            var types = GetAllTypes(compilation.Assembly);
            var immutableArrayType = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1");
            var separatedSyntaxListType =
                compilation.GetTypeByMetadataName("Buckle.CodeAnalysis.Syntax.SeparatedSyntaxList`1");
            var nodeType = compilation.GetTypeByMetadataName("Buckle.CodeAnalysis.Syntax.Node");
            var nodeTypes = types.Where(t => !t.IsAbstract && IsDerivedFrom(t, nodeType) && IsPartial(t));

            string indentString = "    ";
            SourceText sourceText;
            using (var stringWriter = new StringWriter())
            using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
                indentedTextWriter.WriteLine("using System;");
                indentedTextWriter.WriteLine("using System.Collections.Generic;\n");

                using (var namespaceCurly = new CurlyIndenter(
                    indentedTextWriter, "namespace Buckle.CodeAnalysis.Syntax")) {
                    foreach (var type in nodeTypes) {
                        using (var classCurly = new CurlyIndenter(indentedTextWriter, $"partial class {type.Name}"))
                        using (var getChildCurly = new CurlyIndenter(
                            indentedTextWriter, "public override IEnumerable<Node> GetChildren()")) {
                            var properties = type.GetMembers().OfType<IPropertySymbol>();

                            foreach (var property in properties) {
                                if (property.Type is INamedTypeSymbol propertyType) {
                                    if (IsDerivedFrom(property.Type, nodeType)) {
                                        var canBeNull = property.NullableAnnotation == NullableAnnotation.Annotated;
                                        if (canBeNull) {
                                            indentedTextWriter.WriteLine($"if ({property.Name} != null)");
                                            indentedTextWriter.Indent++;
                                        }

                                        indentedTextWriter.WriteLine($"yield return {property.Name};");

                                        if (canBeNull)
                                            indentedTextWriter.Indent--;
                                    } else if (SymbolEqualityComparer.Default.Equals(
                                        propertyType.OriginalDefinition, immutableArrayType) &&
                                        IsDerivedFrom(propertyType.TypeArguments[0], nodeType)) {
                                        indentedTextWriter.WriteLine($"foreach (var child in {property.Name})");
                                        indentedTextWriter.WriteLine($"{indentString}yield return child;");
                                    } else if (SymbolEqualityComparer.Default.Equals(
                                        propertyType.OriginalDefinition, separatedSyntaxListType) &&
                                        IsDerivedFrom(propertyType.TypeArguments[0], nodeType)) {
                                        indentedTextWriter.WriteLine(
                                            $"foreach (var child in {property.Name}.GetWithSeparators())");
                                        indentedTextWriter.WriteLine($"{indentString}yield return child;");
                                    }
                                }
                            }

                            if (properties.ToArray().Length <= 1)
                                indentedTextWriter.WriteLine("return Array.Empty<Node>();");
                        }
                    }
                }

                indentedTextWriter.Flush();
                stringWriter.Flush();
                sourceText = SourceText.From(stringWriter.ToString(), Encoding.UTF8);
            }

            var nodeFileName = nodeType.DeclaringSyntaxReferences.First().SyntaxTree.FilePath;
            var syntaxDirectory = Path.GetDirectoryName(nodeFileName);
            var fileName = Path.Combine(syntaxDirectory, "getchildren.g.cs");

            if (!File.Exists(fileName) || File.ReadAllText(fileName) != sourceText.ToString()) {
                if (!File.Exists(fileName))
                    context.AddSource("generated.cs", sourceText);

                using (var writer = new StreamWriter(fileName))
                    sourceText.Write(writer);
            }
        }

        private bool IsDerivedFrom(ITypeSymbol type, INamedTypeSymbol baseType) {
            while (type != null) {
                if (SymbolEqualityComparer.Default.Equals(type, baseType))
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        private bool IsPartial(INamedTypeSymbol type) {
            foreach (var declaration in type.DeclaringSyntaxReferences) {
                var syntax = declaration.GetSyntax();
                if (syntax is TypeDeclarationSyntax typeDeclaration) {
                    foreach (var modifier in typeDeclaration.Modifiers) {
                        if (modifier.ValueText == "partial")
                            return true;
                    }
                }
            }

            return false;
        }

        private IReadOnlyList<INamedTypeSymbol> GetAllTypes(IAssemblySymbol symbol) {
            var result = new List<INamedTypeSymbol>();
            GetAllTypes(result, symbol.GlobalNamespace);
            return result;
        }

        private void GetAllTypes(List<INamedTypeSymbol> result, INamespaceOrTypeSymbol symbol) {
            if (symbol is INamedTypeSymbol type)
                result.Add(type);

            foreach (var child in symbol.GetMembers())
                if (child is INamespaceOrTypeSymbol nsChild)
                    GetAllTypes(result, nsChild);
        }
    }
}
