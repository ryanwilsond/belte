using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Encased object that can also be a reference to a symbol.
/// </summary>
internal sealed class EvaluatorObject {
    /// <summary>
    /// Creates an EvaluatorObject with a value (not a reference).
    /// In this case EvaluatorObject acts purely as an Object wrapper.
    /// </summary>
    /// <param name="value">Value to store</param>
    internal EvaluatorObject(object value) {
        this.value = value;
        this.isReference = false;
        this.reference = null;
    }

    /// <summary>
    /// Creates an EvaluatorObjet without a value, and instead a reference to a VariableSymbol.
    /// Note that it is not an actual C# reference, just a copy of a symbol stored in the locals or globals dictionary.
    /// </summary>
    /// <param name="reference">Variable to reference (not an explicit reference, passed by reference by default)</param>
    internal EvaluatorObject(VariableSymbol reference) {
        this.value = null;

        if (reference == null) {
            this.isReference = false;
            this.reference = null;
        } else {
            this.isReference = true;
            this.reference = reference;
        }
    }

    /// <summary>
    /// Value of object, only applicable if isReference is set to false.
    /// </summary>
    internal object value { get; set; }

    /// <summary>
    /// If this is to be treated as a reference. If so, value is set to null but ignored.
    /// If value is set to null and isReference is false,
    /// Then it treats value as being the value null, not lacking a value.
    /// </summary>
    internal bool isReference { get; set; }

    /// <summary>
    /// Reference to a symbol stored in the locals or globals dictionary.
    /// Not explicitly a reference, but is passed by reference by default.
    /// </summary>
    /// <value></value>
    internal VariableSymbol reference { get; set; }
}
