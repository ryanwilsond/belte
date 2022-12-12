using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
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
