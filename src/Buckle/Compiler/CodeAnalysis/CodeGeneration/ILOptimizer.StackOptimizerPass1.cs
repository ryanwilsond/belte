using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed class StackOptimizerPass1 : BoundTreeRewriter {
        internal static readonly DummyLocal Empty = new DummyLocal();

        private readonly bool _debugFriendly;
        private readonly ArrayBuilder<(BoundExpression, ExprContext)> _evalStack;
        private readonly Dictionary<DataContainerSymbol, LocalDefUseInfo> _locals;
        // TODO Perf: use SmallDictionary instead
        private readonly Dictionary<object, DummyLocal> _dummyVariables
            = new Dictionary<object, DummyLocal>(ReferenceEqualityComparer.Instance);

        // private int _counter;
        // private ExprContext _context;
        // private BoundDataContainerExpression _assignmentLocal;
        // private int _recursionDepth;

        private StackOptimizerPass1(
            Dictionary<DataContainerSymbol, LocalDefUseInfo> locals,
            ArrayBuilder<(BoundExpression, ExprContext)> evalStack,
            bool debugFriendly) {
            _locals = locals;
            _evalStack = evalStack;
            _debugFriendly = debugFriendly;

            // DeclareLocal(Empty, 0);
            // RecordDummyWrite(Empty);
        }

        internal static BoundNode Analyze(
            BoundNode node,
            Dictionary<DataContainerSymbol, LocalDefUseInfo> locals,
            bool debugFriendly) {
            var evalStack = ArrayBuilder<(BoundExpression, ExprContext)>.GetInstance();
            var analyzer = new StackOptimizerPass1(locals, evalStack, debugFriendly);
            var rewritten = analyzer.Visit(node);
            evalStack.Free();
            return rewritten;
        }
    }
}
