using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed class StackOptimizerPass2 : BoundTreeRewriterWithStackGuard {
        private readonly Dictionary<DataContainerSymbol, LocalDefUseInfo> _info;

        // private int _nodeCounter;

        private StackOptimizerPass2(Dictionary<DataContainerSymbol, LocalDefUseInfo> info) {
            _info = info;
        }

        internal static BoundBlockStatement Rewrite(
            BoundBlockStatement src,
            Dictionary<DataContainerSymbol, LocalDefUseInfo> info) {
            var scheduler = new StackOptimizerPass2(info);
            return (BoundBlockStatement)scheduler.Visit(src);
        }
    }
}
