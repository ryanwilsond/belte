
namespace Buckle.CodeAnalysis;

internal abstract partial class PublicSemanticModel : SemanticModel {
    internal /*sealed override*/ SemanticModel containingPublicModelOrSelf => this;
}
