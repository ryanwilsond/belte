using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding {

    internal sealed class BoundScope {
        private Dictionary<string, Symbol> symbols_;
        public BoundScope parent;

        public BoundScope(BoundScope parent_) {
            parent = parent_;
        }

        public bool TryDeclareFunction(FunctionSymbol symbol) => TryDeclareSymbol(symbol);
        public bool TryDeclareVariable(VariableSymbol symbol) => TryDeclareSymbol(symbol);

        private bool TryDeclareSymbol<TSymbol>(TSymbol symbol) where TSymbol : Symbol {
            if (symbols_ == null)
                symbols_ = new Dictionary<string, Symbol>();
            else if (symbols_.ContainsKey(symbol.name))
                return false;

            symbols_.Add(symbol.name, symbol);
            return true;
        }

        public bool TryLookupFunction(string name, out FunctionSymbol function) => TryLookupSymbol(name, out function);
        public bool TryLookupVariable(string name, out VariableSymbol variable) => TryLookupSymbol(name, out variable);

        private bool TryLookupSymbol<TSymbol>(string name, out TSymbol symbol) where TSymbol : Symbol {
            symbol = null;

            if (symbols_ != null && symbols_.TryGetValue(name, out var declaredSymbol)) {
                if (declaredSymbol is TSymbol matchingSymbol) {
                    symbol = matchingSymbol;
                    return true;
                }

                return false;
            }

            if (parent == null)
                return false;

            return parent.TryLookupSymbol(name, out symbol);
        }

        public ImmutableArray<VariableSymbol> GetDeclaredVariables() => GetDeclaredSymbols<VariableSymbol>();
        public ImmutableArray<FunctionSymbol> GetDeclaredFunctions() => GetDeclaredSymbols<FunctionSymbol>();

        private ImmutableArray<TSymbol> GetDeclaredSymbols<TSymbol>() where TSymbol : Symbol {
            if (symbols_ == null)
                return ImmutableArray<TSymbol>.Empty;

            return symbols_.Values.OfType<TSymbol>().ToImmutableArray();
        }
    }

    internal sealed class BoundGlobalScope {
        public BoundGlobalScope previous { get; }
        public DiagnosticQueue diagnostics { get; }
        public ImmutableArray<FunctionSymbol> functions { get; }
        public ImmutableArray<VariableSymbol> variables { get; }
        public ImmutableArray<BoundStatement> statements { get; }

        public BoundGlobalScope(
            BoundGlobalScope previous_, DiagnosticQueue diagnostics_, ImmutableArray<FunctionSymbol> functions_,
            ImmutableArray<VariableSymbol> variables_, ImmutableArray<BoundStatement> statements_) {
            previous = previous_;
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(diagnostics_);
            functions = functions_;
            variables = variables_;
            statements = statements_;
        }
    }

    internal sealed class BoundProgram {
        public DiagnosticQueue diagnostics { get; }
        public ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies { get; }
        public BoundBlockStatement statement { get; }

        public BoundProgram(
            DiagnosticQueue diagnostics_, ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies_,
            BoundBlockStatement statement_) {
            diagnostics = diagnostics_;
            functionBodies = functionBodies_;
            statement = statement_;
        }
    }
}
