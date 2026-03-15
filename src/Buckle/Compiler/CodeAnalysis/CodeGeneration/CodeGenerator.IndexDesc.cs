using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    private readonly struct IndexDesc {
        internal IndexDesc(int index, ImmutableArray<BoundExpression> initializers) {
            this.index = index;
            this.initializers = initializers;
        }

        internal readonly int index;
        internal readonly ImmutableArray<BoundExpression> initializers;
    }
}
