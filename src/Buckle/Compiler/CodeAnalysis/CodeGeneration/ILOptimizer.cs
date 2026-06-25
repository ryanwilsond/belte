using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed class ILOptimizer {
    internal static BoundBlockStatement Optimize(
        BoundBlockStatement src,
        bool debugFriendly,
        out HashSet<DataContainerSymbol> stackLocals) {
        // TODO
        stackLocals = [];
        // var locals = PooledDictionary<DataContainerSymbol, LocalDefUseInfo>.GetInstance();
        // src = (BoundStatement)StackOptimizerPass1.Analyze(src, locals, debugFriendly);

        // FilterValidStackLocals(locals);

        // BoundStatement result;
        // if (locals.Count == 0) {
        //     stackLocals = null;
        //     result = src;
        // } else {
        //     stackLocals = new HashSet<LocalSymbol>(locals.Keys);
        //     result = StackOptimizerPass2.Rewrite(src, locals);
        // }

        // foreach (var info in locals.Values) {
        //     info.Free();
        // }

        // locals.Free();

        // return result;
        return src;
    }
}
