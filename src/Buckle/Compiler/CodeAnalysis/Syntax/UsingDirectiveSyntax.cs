
namespace Buckle.CodeAnalysis.Syntax;

public partial class UsingDirectiveSyntax {
    internal NameSyntax name => namespaceOrType as NameSyntax;
}
