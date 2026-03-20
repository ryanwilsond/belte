
namespace Buckle.CodeAnalysis;

internal partial class IdentifierCollection {
    private sealed class CaseSensitiveCollection : CollectionBase {
        internal CaseSensitiveCollection(IdentifierCollection identifierCollection) : base(identifierCollection) { }

        public override bool Contains(string item) => _identifierCollection.CaseSensitiveContains(item);
    }
}
