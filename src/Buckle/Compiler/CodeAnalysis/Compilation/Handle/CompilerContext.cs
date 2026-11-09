using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis;

public sealed class CompilerContext {
    private readonly Compilation _compilation;

    public CompilerContext(Compilation compilation) {
        _compilation = compilation;
    }

    public void AddSyntaxTree(SyntaxTree tree) {
        _compilation.AddLateSyntaxTrees([tree]);
    }

    public void AddSyntaxTrees(SyntaxTree[] tree) {
        _compilation.AddLateSyntaxTrees(tree);
    }

    public void AddSourceText(string text) {
        var tree = SyntaxTree.Parse(text, GetParseOptions());
        AddSyntaxTree(tree);
    }

    public ParseOptions GetParseOptions() {
        var options = _compilation.options;
        // TODO Expose command-line preprocessor symbols?
        return Compiler.CreateParseOptions(options.buildMode, options.optimizationLevel == OptimizationLevel.Debug, []);
    }

    public SyntaxTree[] GetSyntaxTrees() {
        return _compilation.syntaxTrees.ToArray();
    }

    public Compilation GetCompilation() {
        return _compilation;
    }

    internal BoundProgram GetBoundProgram() {
        return _compilation.boundProgram;
    }

    public BelteDiagnosticQueue GetMethodDiagnostics() {
        return _compilation.methodDiagnostics;
    }

    internal ImmutableArray<Symbol> GetSymbolsWithName(string name, SymbolFilter filter) {
        return _compilation.GetSymbolsWithName(name, filter).ToImmutableArray();
    }

    internal ImmutableArray<Symbol> GetSymbols(SymbolFilter filter) {
        return _compilation.GetSymbols(filter).ToImmutableArray();
    }

    internal void ReplaceBoundProgram(BoundProgram program, BelteDiagnosticQueue methodDiagnostics) {
        _compilation.ReplaceBoundProgram(program, methodDiagnostics);
    }

    internal void ReplaceBoundProgram(BoundProgram program) {
        _compilation.ReplaceBoundProgram(program, _compilation.methodDiagnostics);
    }

    internal static MethodSymbol SynthesizeEntryPoint(MethodSymbol entryPoint, string newName) {
        var newEntry = new SynthesizedFinishedMethodSymbol(
            new SynthesizedSimpleOrdinaryMethodSymbol(
                newName,
                entryPoint.returnTypeWithAnnotations,
                RefKind.None,
                entryPoint.isStatic ? DeclarationModifiers.Static : DeclarationModifiers.None
            ),
            entryPoint.containingType,
            entryPoint.parameters
        );

        return newEntry;
    }
}
