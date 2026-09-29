using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class HeapObject {
    internal HeapObject(TypeSymbol type, int slotCount) {
        Debug.Assert(!type.ContainsTemplateParameter());
        this.type = type;
        fields = new EvaluatorValue[slotCount];
    }

    internal HeapObject(TypeSymbol type, EvaluatorValue[] fields) {
        Debug.Assert(!type.ContainsTemplateParameter());
        this.type = type;
        this.fields = fields;
    }

    internal TypeSymbol type { get; }

    internal bool markedForCollection { get; set; }

    public EvaluatorValue[] fields { get; }

    public ITypeSymbol publicType => type;
}
