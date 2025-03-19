using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    internal struct ProcessedFieldInitializers {
        internal ImmutableArray<BoundInitializer> boundInitializers { get; set; }

        internal BoundStatement loweredInitializers { get; set; }

        internal bool hasErrors { get; set; }
    }
}
