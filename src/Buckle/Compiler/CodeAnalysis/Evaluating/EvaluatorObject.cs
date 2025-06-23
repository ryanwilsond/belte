using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
public sealed class EvaluatorObject {
    public object value { get; internal set; }

    public bool isReference { get; internal set; }

    public ISymbol publicReference => referenceSymbol;

    public Dictionary<ISymbol, EvaluatorObject> publicMembers
        => members?.ToDictionary(item => (ISymbol)item.Key, item => item.Value);

    public ITypeSymbol publicType => type;

    internal bool isExplicitReference { get; set; }

    internal Symbol referenceSymbol { get; set; }

    internal EvaluatorObject reference { get; set; }

    internal Dictionary<Symbol, EvaluatorObject> members { get; set; }

    internal TypeSymbol type { get; set; }

    internal EvaluatorObject parent { get; set; }

    internal int? heapPointer { get; set; }

    internal bool escapes { get; set; }

    internal int refCount { get; set; }

    internal bool markedForCollection { get; set; }

    internal static EvaluatorObject GetInstance() {
        return new EvaluatorObject();
    }

    internal static EvaluatorObject GetInstance(object value, TypeSymbol type) {
        var instance = new EvaluatorObject {
            value = value,
            type = type
        };

        return instance;
    }

    internal static EvaluatorObject GetInstance(Dictionary<Symbol, EvaluatorObject> members, TypeSymbol type) {
        var instance = new EvaluatorObject {
            members = members,
            type = type
        };

        return instance;
    }

    internal static EvaluatorObject GetInstance(
        Symbol referenceSymbol,
        EvaluatorObject reference,
        TypeSymbol type,
        bool isExplicitReference = false) {
        var instance = new EvaluatorObject {
            isReference = true,
            referenceSymbol = referenceSymbol,
            reference = reference,
            isExplicitReference = isExplicitReference,
            type = type
        };

        return instance;
    }
}
