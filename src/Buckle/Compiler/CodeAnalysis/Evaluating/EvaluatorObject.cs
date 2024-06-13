using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
public sealed class EvaluatorObject {
    internal static EvaluatorObject Null => new EvaluatorObject(value: null);

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a null value.
    /// </summary>
    internal EvaluatorObject() {
        value = null;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        members = null;
        trueType = null;
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
        trueType = null;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> without a value, and instead a list of members.
    /// </summary>
    /// <param name="members">Members to contain by this.</param>
    internal EvaluatorObject(Dictionary<Symbol, EvaluatorObject> members, BoundType trueType) {
        value = null;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        this.members = members;
        this.trueType = trueType;
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
    internal EvaluatorObject(VariableSymbol reference, bool isExplicitReference = false) {
        value = null;
        isReference = true;
        this.reference = reference;
        this.isExplicitReference = isExplicitReference;
        members = null;
        trueType = null;
    }

    internal object value { get; set; }

    internal bool isReference { get; set; }

    internal bool isExplicitReference { get; set; }

    internal VariableSymbol reference { get; set; }

    internal Dictionary<Symbol, EvaluatorObject> members { get; set; }

    internal BoundType trueType { get; set; }
}
