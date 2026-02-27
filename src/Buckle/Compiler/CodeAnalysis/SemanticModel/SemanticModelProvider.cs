using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis;

internal abstract class SemanticModelProvider {
    internal abstract SemanticModel GetSemanticModel(SyntaxTree tree, Compilation compilation);
}
