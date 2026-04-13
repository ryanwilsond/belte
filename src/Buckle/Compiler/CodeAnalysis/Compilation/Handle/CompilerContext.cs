using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

public sealed class CompilerContext {
    private readonly Compilation _compilation;

    internal CompilerContext(Compilation compilation) {
        _compilation = compilation;
    }

    internal void AddSyntaxTree(SyntaxTree tree) {
        _compilation.AddLateSyntaxTrees([tree]);
    }

    internal void AddSyntaxTrees(List<SyntaxTree> tree) {
        _compilation.AddLateSyntaxTrees(tree);
    }

    internal BoundProgram GetBoundProgram() {
        return _compilation.boundProgram;
    }

    internal BelteDiagnosticQueue GetMethodDiagnostics() {
        return _compilation.methodDiagnostics;
    }

    internal ImmutableArray<Symbol> GetSymbolsWithName(string name, SymbolFilter filter) {
        return _compilation.GetSymbolsWithName(name, filter).ToImmutableArray();
    }

    internal ImmutableArray<Symbol> GetSymbols(SymbolFilter filter) {
        return _compilation.GetSymbols(filter).ToImmutableArray();
    }

    internal Compilation GetCompilation() {
        return _compilation;
    }

    internal void ReplaceBoundProgram(BoundProgram program, BelteDiagnosticQueue methodDiagnostics) {
        _compilation.ReplaceBoundProgram(program, methodDiagnostics);
    }

    internal void ReplaceBoundProgram(BoundProgram program) {
        _compilation.ReplaceBoundProgram(program, _compilation.methodDiagnostics);
    }
}
