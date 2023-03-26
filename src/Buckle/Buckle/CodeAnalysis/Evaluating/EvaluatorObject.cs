using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
internal sealed class EvaluatorObject {
    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a null value.
    /// </summary>
    internal EvaluatorObject() {
        this.value = null;
        this.isReference = false;
        this.reference = null;
        this.isExplicitReference = false;
        this.members = null;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a value (not a reference).
    /// In this case <see cref="EvaluatorObject" /> acts purely as an Object wrapper.
    /// </summary>
    /// <param name="value">Value to store.</param>
    internal EvaluatorObject(object value) {
        this.value = value;
        this.isReference = false;
        this.reference = null;
        this.isExplicitReference = false;
        this.members = null;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> without a value, and instead a list of members.
    /// </summary>
    /// <param name="members">Members to contain by this.</param>
    internal EvaluatorObject(Dictionary<Symbol, EvaluatorObject> members) {
        this.value = null;
        this.isReference = false;
        this.reference = null;
        this.isExplicitReference = false;
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
        Dictionary<VariableSymbol, EvaluatorObject> referenceScope = null) {
        this.value = null;
        this.isReference = true;
        this.reference = reference;
        this.referenceScope = referenceScope;
        this.isExplicitReference = isExplicitReference;
        this.members = null;
    }

    /// <summary>
    /// Value of object, only applicable if <see cref="isReference" /> is set to false.
    /// </summary>
    internal object value { get; set; }

    /// <summary>
    /// If this is to be treated as a reference. If so, value is set to null but ignored.
    /// If value is set to null and <see cref="isReference" /> is false,
    /// Then it treats value as being the value null, not lacking a value.
    /// </summary>
    internal bool isReference { get; set; }

    /// <summary>
    /// The local scope that the reference (if applicable) is referring to.
    /// </summary>
    internal Dictionary<VariableSymbol, EvaluatorObject> referenceScope { get; set; }

    /// <summary>
    /// If the reference is an explicit reference expression, or if it is just a normal variable.
    /// </summary>
    internal bool isExplicitReference { get; set; }

    /// <summary>
    /// Reference to a <see cref="VariableSymbol" /> stored in the locals or globals dictionary.
    /// Not explicitly a reference, but is passed by reference by default.
    /// </summary>
    internal VariableSymbol reference { get; set; }

    /// <summary>
    /// Members stored by this.
    /// </summary>
    internal Dictionary<Symbol, EvaluatorObject> members { get; set; }
}
