
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a local or global variable in a method body.
/// </summary>
///
public interface IVariableSymbol : ISymbol {
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    // Changes to this public interface should remain synchronized with the BoundType class.
    // Do not make any changes to this public interface without making the corresponding change to the BoundType class.
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

    /// <summary>
    /// The language type, not the <see cref="Syntax.SyntaxNode" /> type.
    /// </summary>
    public ITypeSymbol typeSymbol { get; }

    /// <summary>
    /// If the type was assumed by the var or let keywords.
    /// </summary>
    public bool isImplicit { get; }

    /// <summary>
    /// If the type is an unchanging reference type.
    /// </summary>
    public bool isConstantReference { get; }

    /// <summary>
    /// If the type is a reference type.
    /// </summary>
    public bool isReference { get; }

    /// <summary>
    /// If the type is explicitly a reference expression, versus a reference type.
    /// </summary>
    public bool isExplicitReference { get; }

    /// <summary>
    /// If the value this type is referring to is only defined once.
    /// </summary>
    public bool isConstant { get; }

    /// <summary>
    /// If the value this type is referring to can be null.
    /// </summary>
    public bool isNullable { get; }

    /// <summary>
    /// If the type was assumed from a literal.
    /// </summary>
    public bool isLiteral { get; }

    /// <summary>
    /// Dimensions of the type, 0 if not an array
    /// </summary>
    public int dimensions { get; }
}
