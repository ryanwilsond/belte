using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private ref struct PlaceholderRegion {
        private readonly RefSafetyAnalysis _analysis;
        private readonly ArrayBuilder<(BoundValuePlaceholder, uint)> _placeholders;

        public PlaceholderRegion(RefSafetyAnalysis analysis, ArrayBuilder<(BoundValuePlaceholder, uint)> placeholders) {
            _analysis = analysis;
            _placeholders = placeholders;

            foreach (var (placeholder, valEscapeScope) in placeholders)
                _analysis.AddPlaceholderScope(placeholder, valEscapeScope);
        }

        public void Dispose() {
            foreach (var (placeholder, _) in _placeholders)
                _analysis.RemovePlaceholderScope(placeholder);

            _placeholders.Free();
        }
    }
}
