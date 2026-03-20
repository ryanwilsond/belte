
namespace Buckle.CodeAnalysis;

internal partial class IdentifierCollection {
    private sealed class CaseInsensitiveCollection : CollectionBase {
        internal CaseInsensitiveCollection(IdentifierCollection identifierCollection) : base(identifierCollection) { }

        public override bool Contains(string item) => _identifierCollection.CaseInsensitiveContains(item);
    }
}
