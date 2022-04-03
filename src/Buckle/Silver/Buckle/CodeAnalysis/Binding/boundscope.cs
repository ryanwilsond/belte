using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding {

    internal sealed class BoundScope {
        private Dictionary<string, VariableSymbol> variables_;
        public BoundScope parent;

        public BoundScope(BoundScope parent_) {
            variables_ = new Dictionary<string, VariableSymbol>();
            parent = parent_;
        }

        public bool TryDeclare(VariableSymbol variable) {
            if (variables_.ContainsKey(variable.name)) return false;
            variables_.Add(variable.name, variable);
            return true;
        }

        public bool TryLookup(string name, out VariableSymbol variable) {
            if (variables_.TryGetValue(name, out variable)) return true;
            if (parent == null) return false;
            return parent.TryLookup(name, out variable);
        }

        public ImmutableArray<VariableSymbol> GetDeclaredVariables() {
            return variables_.Values.ToImmutableArray();
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
