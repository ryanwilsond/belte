using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal interface ISynthesizedTemplate {
    ISymbolWithTemplates unexpandedSymbol { get; }

    Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> replacementTemplateParameters { get; }
}
