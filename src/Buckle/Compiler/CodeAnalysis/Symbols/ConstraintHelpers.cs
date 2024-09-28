using System.Collections.Generic;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

// Eventually this will be populated with more thorough checks
internal static class ConstraintsHelpers {
    internal static TypeParameterBounds ResolveBounds(
        this SourceTemplateParameterSymbolBase templateParameter,
        List<TemplateParameterSymbol> inProgress,
        Compilation currentCompilation,
        BelteDiagnosticQueue diagnostics) {
        // TODO
        return null;
    }
}
