using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding {

    internal sealed class BoundScope {
        private Dictionary<string, VariableSymbol> variables_;
        private Dictionary<string, FunctionSymbol> functions_ = new Dictionary<string, FunctionSymbol>();
        public BoundScope parent;

        public BoundScope(BoundScope parent_) {
            parent = parent_;
        }

        public bool TryDeclareVariable(VariableSymbol variable) {
            if (variables_ == null)
                variables_ = new Dictionary<string, VariableSymbol>();

            if (variables_.ContainsKey(variable.name)) return false;
            variables_.Add(variable.name, variable);
            return true;
        }

        public bool TryLookupVariable(string name, out VariableSymbol variable) {
            variable = null;

            if (variables_ != null && variables_.TryGetValue(name, out variable)) return true;
            if (parent == null) return false;

            return parent.TryLookupVariable(name, out variable);
        }

        public bool TryDeclareFunction(FunctionSymbol function) {
            if (functions_ == null)
                functions_ = new Dictionary<string, FunctionSymbol>();

            if (functions_.ContainsKey(function.name)) return false;
            functions_.Add(function.name, function);
            return true;
        }

        public bool TryLookupFunction(string name, out FunctionSymbol function) {
            function = null;

            if (functions_ != null && functions_.TryGetValue(name, out function)) return true;
            if (parent == null) return false;

            return parent.TryLookupFunction(name, out function);
        }

        public ImmutableArray<VariableSymbol> GetDeclaredVariables() {
            if (variables_ == null)
                return ImmutableArray<VariableSymbol>.Empty;

            return variables_.Values.ToImmutableArray();
        }

        public ImmutableArray<FunctionSymbol> GetDeclaredFunctions() {
            if (functions_ == null)
                return ImmutableArray<FunctionSymbol>.Empty;

            return functions_.Values.ToImmutableArray();
        }
    }

    internal sealed class BoundGlobalScope {
        public BoundGlobalScope previous { get; }
        public DiagnosticQueue diagnostics { get; }
        public ImmutableArray<VariableSymbol> variables { get; }
        public ImmutableArray<BoundStatement> statements { get; }

        public BoundGlobalScope(
            BoundGlobalScope previous_, DiagnosticQueue diagnostics_,
            ImmutableArray<VariableSymbol> variables_, ImmutableArray<BoundStatement> statements_) {
            previous = previous_;
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(diagnostics_);
            variables = variables_;
            statements = statements_;
        }
    }
}
