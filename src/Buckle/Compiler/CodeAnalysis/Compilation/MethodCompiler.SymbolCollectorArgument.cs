using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class MethodCompiler {
    internal struct SymbolCollectorArgument {
        internal MethodCompiler compiler;
        internal HashSet<NamedTypeSymbol> visited;
    }
}
