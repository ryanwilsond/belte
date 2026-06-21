
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class MutableTemplateMap : TemplateMap {
    internal MutableTemplateMap() : base([]) { }

    internal void Add(TemplateParameterSymbol key, TypeOrConstant value) {
        _mapping.Add(key, value);
    }
}
