using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// All builtin functions (included by default).
/// </summary>
internal static class BuiltinFunctions {
    /// <summary>
    /// Print function, writes text to stdout (no line break).
    /// </summary>
    internal static readonly FunctionSymbol Print = new FunctionSymbol("Print",
        ImmutableArray.Create(new ParameterSymbol("text", BoundTypeClause.NullableAny, 0)),
        new BoundTypeClause(TypeSymbol.Void));

    /// <summary>
    /// PrintLine function, writes text to stdout (with line break).
    /// </summary>
    internal static readonly FunctionSymbol PrintLine = new FunctionSymbol("PrintLine",
        ImmutableArray.Create(new ParameterSymbol("text", BoundTypeClause.NullableAny, 0)),
        new BoundTypeClause(TypeSymbol.Void));

    /// <summary>
    /// Input function, gets text input from stdin. Waits until enter is pressed.
    /// </summary>
    internal static readonly FunctionSymbol Input = new FunctionSymbol("Input",
        ImmutableArray<ParameterSymbol>.Empty, BoundTypeClause.String);

    /// <summary>
    /// RandInt function, gets a random integer with a maximum (minimum is always 0).
    /// </summary>
    internal static readonly FunctionSymbol RandInt = new FunctionSymbol("RandInt",
        ImmutableArray.Create(new ParameterSymbol("max", BoundTypeClause.NullableInt, 0)), BoundTypeClause.Int);

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Any type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueAny = new FunctionSymbol("Value",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableAny, 0)), BoundTypeClause.Any);

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueBool = new FunctionSymbol("Value",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableBool, 0)), BoundTypeClause.Bool);

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueDecimal = new FunctionSymbol("Value",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableDecimal, 0)),
        BoundTypeClause.Decimal);
    internal static readonly FunctionSymbol ValueInt = new FunctionSymbol("Value",

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// String type overload.
    /// </summary>
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableInt, 0)), BoundTypeClause.Int);
    internal static readonly FunctionSymbol ValueString = new FunctionSymbol("Value",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableString, 0)), BoundTypeClause.String);

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// </summary>
    internal static readonly FunctionSymbol HasValue = new FunctionSymbol("HasValue",
        ImmutableArray.Create(new ParameterSymbol("value", BoundTypeClause.NullableAny, 0)), BoundTypeClause.Bool);

    /// <summary>
    /// Gets all builtin functions.
    /// </summary>
    /// <returns>All builtins, calling code should not depend on order</returns>
    internal static IEnumerable<FunctionSymbol> GetAll()
        => typeof(BuiltinFunctions).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(FunctionSymbol))
        .Select(f => (FunctionSymbol)f.GetValue(null));
}

/// <summary>
/// Parameter symbol (used in function symbols).
/// </summary>
internal sealed class ParameterSymbol : LocalVariableSymbol {
    /// <summary>
    /// Creates a parameter symbol.
    /// </summary>
    /// <param name="name">Name of parameter</param>
    /// <param name="typeClause">Full type clause of parameter</param>
    /// <param name="ordinal">Index of which parameter it is (zero indexed)</param>
    internal ParameterSymbol(string name, BoundTypeClause typeClause, int ordinal) : base(name, typeClause, null) {
        this.ordinal = ordinal;
    }

    /// <summary>
    /// Type of symbol (see SymbolType).
    /// </summary>
    internal override SymbolType type => SymbolType.Parameter;

    /// <summary>
    /// Ordinal of this parameter.
    /// </summary>
    internal int ordinal { get; }
}

/// <summary>
/// A function symbol.
/// </summary>
internal sealed class FunctionSymbol : Symbol {
    /// <summary>
    /// Creates a function symbol.
    /// </summary>
    /// <param name="name">Name of function</param>
    /// <param name="parameters">Parameters of function</param>
    /// <param name="typeClause">Type clause of return type</param>
    /// <param name="declaration">Declaration of function</param>
    internal FunctionSymbol(
        string name, ImmutableArray<ParameterSymbol> parameters,
        BoundTypeClause typeClause, FunctionDeclaration declaration = null)
        : base(name) {
        this.typeClause = typeClause;
        this.parameters = parameters;
        this.declaration = declaration;
    }

    /// <summary>
    /// All parameters (see ParameterSymbol).
    /// </summary>
    internal ImmutableArray<ParameterSymbol> parameters { get; }

    /// <summary>
    /// Type clause of function return type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Declaration of function (see FunctionDeclaration).
    /// </summary>
    internal FunctionDeclaration declaration { get; }

    /// <summary>
    /// Type of symbol (see SymbolType).
    /// </summary>
    internal override SymbolType type => SymbolType.Function;
}
