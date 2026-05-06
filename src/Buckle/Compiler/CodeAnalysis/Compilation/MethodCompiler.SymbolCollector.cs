using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class MethodCompiler {
    internal sealed class SymbolCollector : BoundTreeWalker {
        private readonly MethodCompiler _compiler;
        private readonly HashSet<NamedTypeSymbol> _visited;
        private readonly SymbolCollectorArgument _argument;

        private SymbolCollector(MethodCompiler compiler) {
            _compiler = compiler;
            _visited = [];
            _argument = new SymbolCollectorArgument() { compiler = _compiler, visited = _visited };
        }

        internal static void Collect(MethodCompiler compiler, BoundBlockStatement body) {
            var collector = new SymbolCollector(compiler);
            collector.Visit(body);
        }

        internal override BoundNode Visit(BoundNode node) {
            if (node is BoundExpression expression && expression.type is not null)
                expression.type.VisitType(VisitTypePredicate, _argument);

            return base.Visit(node);
        }

        internal static bool VisitTypePredicate(
            TypeSymbol type,
            SymbolCollectorArgument arg,
            bool canDigThroughNullable = true) {
            if (type is NamedTypeSymbol t && t.IsFromCompilation(arg.compiler._compilation)) {
                if (arg.visited.Add(t)) {
                    var compiler = arg.compiler;

                    if (!compiler._types.Contains(t) && !PassesFilter(compiler._filter, t)) {
                        if (compiler._compilation.options.concurrentBuild)
                            compiler.Enqueue(() => compiler.CompileNamedType(t));
                        else
                            compiler.CompileNamedType(t);
                    }
                }
            }

            return false;
        }
    }
}
