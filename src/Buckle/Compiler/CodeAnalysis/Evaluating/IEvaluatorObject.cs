using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

public interface IEvaluatorObject {
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
    internal Dictionary<IVariableSymbol, IEvaluatorObject> referenceScope { get; set; }

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

    /// <summary>
    /// The true instance type of the object if not a primitive.
    /// </summary>
    internal BoundType trueType { get; set; }
}
