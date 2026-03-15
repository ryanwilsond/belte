using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter {
    internal sealed class Scope {
        internal readonly Scope parent;
        internal readonly ArrayBuilder<Scope> nestedScopes = ArrayBuilder<Scope>.GetInstance();
        internal readonly ArrayBuilder<NestedFunction> nestedFunctions = ArrayBuilder<NestedFunction>.GetInstance();
        internal readonly SetWithInsertionOrder<Symbol> declaredLocals = [];
        internal readonly BoundNode boundNode;
        internal readonly NestedFunction containingFunction;

        internal ClosureEnvironment declaredEnvironment;

        internal Scope(Scope parent, BoundNode boundNode, NestedFunction containingFunction) {
            this.parent = parent;
            this.boundNode = boundNode;
            this.containingFunction = containingFunction;
        }

        internal bool canMergeWithParent { get; set; } = true;

        internal void Free() {
            foreach (var scope in nestedScopes)
                scope.Free();

            nestedScopes.Free();

            foreach (var function in nestedFunctions)
                function.Free();

            nestedFunctions.Free();
        }

        public override string ToString() {
            return boundNode.syntax.ToString();
        }
    }
}
