using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal interface ISynthesizedTemplate<T> where T : ISymbolWithTemplates {
    T unexpandedSymbol { get; }

    Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> replacementTemplateParameters { get; }
}
