
namespace Buckle.CodeAnalysis.Syntax;

public partial class TypeDeclarationSyntax {
    internal int arity => templateParameterList?.parameters?.Count ?? 0;
}
