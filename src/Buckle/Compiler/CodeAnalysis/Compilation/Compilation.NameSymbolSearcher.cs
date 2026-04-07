
namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    private class NameSymbolSearcher : AbstractSymbolSearcher {
        private readonly string _name;

        internal NameSymbolSearcher(
            Compilation compilation,
            SymbolFilter filter,
            string name)
            : base(compilation, filter) {
            _name = name;
        }

        private protected override bool ShouldCheckTypeForMembers(MergedTypeDeclaration current) {
            foreach (var typeDecl in current.declarations) {
                if (typeDecl.memberNames.Value.Contains(_name))
                    return true;
            }

            return false;
        }

        private protected override bool Matches(string name) {
            return _name == name;
        }
    }
}
