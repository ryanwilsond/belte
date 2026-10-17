using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class MethodCompiler {
    internal sealed class SymbolCollector : BoundTreeWalker {
        private readonly MethodCompiler _compiler;
        private readonly HashSet<NamedTypeSymbol> _visited;
        private readonly SymbolCollectorArgument _argument;
        private readonly Func<TypeSymbol, SymbolCollectorArgument, bool, bool> _predicate;

        private SymbolCollector(
            MethodCompiler compiler,
            Func<TypeSymbol, SymbolCollectorArgument, bool, bool> predicate) {
            _compiler = compiler;
            _visited = [];
            _argument = new SymbolCollectorArgument() { compiler = _compiler, visited = _visited };
            _predicate = predicate;
        }

        internal static HashSet<NamedTypeSymbol> Collect(
            MethodCompiler compiler,
            BoundBlockStatement body,
            Func<TypeSymbol, SymbolCollectorArgument, bool, bool> predicate) {
            var collector = new SymbolCollector(compiler, predicate);
            collector.Visit(body);
            return collector._visited;
        }

        internal override BoundNode Visit(BoundNode node) {
            if (node is BoundExpression expression && expression.type is not null)
                expression.type.VisitType(_predicate, _argument);

            return base.Visit(node);
        }

        internal static bool VisitTypePredicateCollectAndEnqueue(TypeSymbol type, SymbolCollectorArgument arg, bool _) {
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

        internal static bool VisitTypePredicateJustCollect(TypeSymbol type, SymbolCollectorArgument arg, bool _) {
            if (type is NamedTypeSymbol t && t.IsFromCompilation(arg.compiler._compilation))
                arg.visited.Add(t);

            return false;
        }
    }
}
