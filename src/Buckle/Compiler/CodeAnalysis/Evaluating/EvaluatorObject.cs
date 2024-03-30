using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
internal sealed class EvaluatorObject : IEvaluatorObject {
    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a null value.
    /// </summary>
    internal EvaluatorObject() {
        value = null;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        members = null;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a value (not a reference).
    /// In this case <see cref="EvaluatorObject" /> acts purely as an Object wrapper.
    /// </summary>
    /// <param name="value">Value to store.</param>
    internal EvaluatorObject(object value) {
        this.value = value;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        members = null;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> without a value, and instead a list of members.
    /// </summary>
    /// <param name="members">Members to contain by this.</param>
    internal EvaluatorObject(Dictionary<Symbol, EvaluatorObject> members) {
        value = null;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        this.members = members;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> without a value, and instead a reference to member of
    /// a <see cref="VariableSymbol" />.
    /// Note that it is not an actual C# reference, just a copy of a <see cref="VariableSymbol" /> stored in the locals
    /// or globals dictionary.
    /// </summary>
    /// <param name="reference">
    /// <see cref="VariableSymbol" /> to reference (not an explicit reference, passed by
    /// reference by default).
    /// </param>
    /// <param name="isExplicitReference">
    /// If this is just a variable, or if it explicitly a reference expression.
    /// </param>
    internal EvaluatorObject(
        VariableSymbol reference, bool isExplicitReference = false,
        Dictionary<IVariableSymbol, IEvaluatorObject> referenceScope = null) {
        value = null;
        isReference = true;
        this.reference = reference;
        this.referenceScope = referenceScope;
        this.isExplicitReference = isExplicitReference;
        members = null;
    }

    public object value { get; set; }

    public bool isReference { get; set; }

    public Dictionary<IVariableSymbol, IEvaluatorObject> referenceScope { get; set; }

    public bool isExplicitReference { get; set; }

    public VariableSymbol reference { get; set; }

    public Dictionary<Symbol, EvaluatorObject> members { get; set; }
}
