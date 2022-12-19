using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased Object that can also be a reference to a <see cref="VariableSymbol" />.
/// </summary>
internal sealed class EvaluatorObject {
    /// <summary>
    /// Creates an <see cref="EvaluatorObject" /> with a value (not a reference).
    /// In this case <see cref="EvaluatorObject" /> acts purely as an Object wrapper.
    /// </summary>
    /// <param name="value">Value to store.</param>
    internal EvaluatorObject(object value) {
        this.value = value;
        this.isReference = false;
        this.reference = null;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObjet" /> without a value, and instead a reference to
    /// a <see cref="VariableSymbol" />.
    /// Note that it is not an actual C# reference, just a copy of a <see cref="VariableSymbol" /> stored in the locals
    /// or globals dictionary.
    /// </summary>
    /// <param name="reference"><see cref="VariableSymbol" /> to reference (not an explicit reference, passed by
    /// reference by default).</param>
    internal EvaluatorObject(VariableSymbol reference, bool explicitReference=false) {
        this.value = null;

        if (reference == null) {
            this.isReference = false;
            this.reference = null;
        } else {
            this.isReference = true;
            this.reference = reference;
        }

        this.fieldReference = null;
        this.isExplicitReference = explicitReference;
    }

    /// <summary>
    /// Creates an <see cref="EvaluatorObjet" /> without a value, and instead a reference to member of
    /// a <see cref="VariableSymbol" />.
    /// Note that it is not an actual C# reference, just a copy of a <see cref="VariableSymbol" /> stored in the locals
    /// or globals dictionary.
    /// </summary>
    /// <param name="reference">
    /// <see cref="VariableSymbol" /> to reference (not an explicit reference, passed by
    /// reference by default).
    /// </param>
    /// <param name="fieldReference"><see cref="FieldSymbol" /> to reference.</param>
    internal EvaluatorObject(VariableSymbol reference, FieldSymbol fieldReference) {
        this.value = null;
        this.isReference = true;
        this.reference = reference;
        this.fieldReference = fieldReference;
    }

    /// <summary>
    /// Value of object, only applicable if <see cref="EvaluatorObject.isReference" /> is set to false.
    /// </summary>
    internal object value { get; set; }

    /// <summary>
    /// If this is to be treated as a reference. If so, value is set to null but ignored.
    /// If value is set to null and <see cref="EvaluatorObject.isReference" /> is false,
    /// Then it treats value as being the value null, not lacking a value.
    /// </summary>
    internal bool isReference { get; set; }

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
    /// Reference to a <see cref="FieldSymbol" /> member of <see cref="EvaluatorObject.reference" />.
    /// </summary>
    internal FieldSymbol fieldReference { get; set; }
}
