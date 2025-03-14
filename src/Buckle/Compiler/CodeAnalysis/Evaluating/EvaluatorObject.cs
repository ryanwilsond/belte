using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
public sealed class EvaluatorObject {
    internal static EvaluatorObject Null => new EvaluatorObject(value: null, type: null);

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a value (not a reference).
    /// In this case <see cref="EvaluatorObject" /> acts purely as an Object wrapper.
    /// </summary>
    /// <param name="value">Value to store.</param>
    internal EvaluatorObject(object value, TypeSymbol type) {
        this.value = value;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        members = null;
        this.type = type;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> without a value, and instead a list of members.
    /// </summary>
    /// <param name="members">Members to contain by this.</param>
    internal EvaluatorObject(Dictionary<Symbol, EvaluatorObject> members, TypeSymbol type) {
        value = null;
        isReference = false;
        reference = null;
        isExplicitReference = false;
        this.members = members;
        this.type = type;
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
    internal EvaluatorObject(Symbol reference, TypeSymbol type, bool isExplicitReference = false) {
        value = null;
        isReference = true;
        this.reference = reference;
        this.isExplicitReference = isExplicitReference;
        members = null;
        this.type = type;
    }

    public object value { get; internal set; }

    public bool isReference { get; internal set; }

    public ISymbol publicReference => reference;

    public Dictionary<ISymbol, EvaluatorObject> publicMembers
        => members?.ToDictionary(item => (ISymbol)item.Key, item => item.Value);

    public ITypeSymbol publicType => type;

    internal bool isExplicitReference { get; set; }

    internal Symbol reference { get; set; }

    internal Dictionary<Symbol, EvaluatorObject> members { get; set; }

    internal TypeSymbol type { get; set; }

    /// <summary>
    /// Checks if this and another EvaluatorObject's values equal.
    /// </summary>
    internal bool ValueEquals(EvaluatorObject other) {
        // TODO should probably rework this
        if ((type is null) != (other.type is null))
            return false;

        if (type is not null) {
            if (!type.Equals(other.type))
                return false;
        }

        if (isReference) {
            if (!other.isReference)
                return false;

            if (reference != other.reference)
                return false;
        } else if (members is null) {
            if (other.isReference || (other.members is not null))
                return false;

            if (!value.Equals(other.value))
                return false;
        } else {
            var memberValues = members.Values.ToArray();
            var otherValues = other.members.Values.ToArray();

            for (var i = 0; i < memberValues.Length; i++) {
                if (!memberValues[i].ValueEquals(otherValues[i]))
                    return false;
            }
        }

        return true;
    }
}
