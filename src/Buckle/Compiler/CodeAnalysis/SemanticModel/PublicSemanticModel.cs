
namespace Buckle.CodeAnalysis;

internal abstract partial class PublicSemanticModel : CSharpSemanticModel {
    internal sealed override SemanticModel containingPublicModelOrSelf => this;
}
