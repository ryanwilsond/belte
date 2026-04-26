using System;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    private class PredicateSymbolSearcher : AbstractSymbolSearcher {
        private readonly Func<string, bool> _predicate;

        internal PredicateSymbolSearcher(
            Compilation compilation,
            SymbolFilter filter,
            Func<string, bool> predicate)
            : base(compilation, filter) {
            _predicate = predicate;
        }

        private protected override bool ShouldCheckTypeForMembers(MergedTypeDeclaration current) {
            return true;
        }

        private protected override bool Matches(string name) {
            return _predicate(name);
        }
    }
}
