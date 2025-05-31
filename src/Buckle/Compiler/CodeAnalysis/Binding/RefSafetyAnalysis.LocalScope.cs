using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private ref struct LocalScope {
        private readonly RefSafetyAnalysis _analysis;
        private readonly ImmutableArray<DataContainerSymbol> _locals;

        public LocalScope(RefSafetyAnalysis analysis, ImmutableArray<DataContainerSymbol> locals) {
            _analysis = analysis;
            _locals = locals;
            _analysis._localScopeDepth++;

            foreach (var local in locals)
                _analysis.AddLocalScopes(local, _analysis._localScopeDepth, CallingMethodScope);
        }

        public void Dispose() {
            foreach (var local in _locals)
                _analysis.RemoveLocalScopes(local);

            _analysis._localScopeDepth--;
        }
    }
}
