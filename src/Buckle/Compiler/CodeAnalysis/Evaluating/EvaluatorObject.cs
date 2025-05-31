using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
public sealed class EvaluatorObject {
    // private static readonly ObjectPool<EvaluatorObject> PoolInstance = CreatePool();
    // private readonly ObjectPool<EvaluatorObject> _pool;

    // private EvaluatorObject(ObjectPool<EvaluatorObject> pool) {
    //     _pool = pool;
    // }

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

    // internal bool isPersistent { get; set; }

    internal static EvaluatorObject GetInstance() {
        // return PoolInstance.Allocate();
        return new EvaluatorObject();
    }

    internal static EvaluatorObject GetInstance(object value, TypeSymbol type) {
        // var instance = PoolInstance.Allocate();
        var instance = new EvaluatorObject();
        instance.value = value;
        instance.type = type;
        return instance;
    }

    internal static EvaluatorObject GetInstance(Dictionary<Symbol, EvaluatorObject> members, TypeSymbol type) {
        // var instance = PoolInstance.Allocate();
        var instance = new EvaluatorObject();
        instance.members = members;
        instance.type = type;
        return instance;
    }

    internal static EvaluatorObject GetInstance(
        Symbol referenceSymbol,
        EvaluatorObject reference,
        TypeSymbol type,
        bool isExplicitReference = false) {
        // var instance = PoolInstance.Allocate();
        var instance = new EvaluatorObject();
        instance.isReference = true;
        instance.referenceSymbol = referenceSymbol;
        instance.reference = reference;
        instance.isExplicitReference = isExplicitReference;
        instance.type = type;
        return instance;
    }

    // internal void Free() {
    //     Reset();
    //     _pool.Free(this);
    // }

    // private void Reset() {
    //     value = null;
    //     isReference = false;
    //     reference = null;
    //     referenceSymbol = null;
    //     isExplicitReference = false;
    //     type = null;

    //     if (members is not null) {
    //         foreach (var member in members.Values)
    //             member.Free();

    //         members = null;
    //     }
    // }

    // private static ObjectPool<EvaluatorObject> CreatePool() {
    //     ObjectPool<EvaluatorObject> pool = null;
    //     pool = new ObjectPool<EvaluatorObject>(() => new EvaluatorObject(pool), 512);
    //     return pool;
    // }
}
