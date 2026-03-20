using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class LocalFunctionRewriter {
    internal sealed class NestedFunction {
        internal readonly MethodSymbol originalMethodSymbol;
        internal readonly SyntaxReference blockSyntax;
        internal readonly PooledHashSet<Symbol> capturedVariables = PooledHashSet<Symbol>.GetInstance();
        internal readonly ArrayBuilder<ClosureEnvironment> capturedEnvironments
            = ArrayBuilder<ClosureEnvironment>.GetInstance();

        internal ClosureEnvironment containingEnvironment;
        internal SynthesizedClosureMethod synthesizedLoweredMethod;

        internal NestedFunction(MethodSymbol symbol, SyntaxReference blockSyntax) {
            originalMethodSymbol = symbol;
            this.blockSyntax = blockSyntax;
        }

        internal bool capturesThis { get; set; }

        internal void Free() {
            capturedVariables.Free();
            capturedEnvironments.Free();
        }
    }
}
