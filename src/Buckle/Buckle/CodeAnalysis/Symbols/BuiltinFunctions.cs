using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// All builtin functions (included by default).
/// </summary>
internal static class BuiltinFunctions {
    /// <summary>
    /// Print function, writes text to stdout (no line break).
    /// </summary>
    internal static readonly FunctionSymbol Print = new FunctionSymbol(
        "Print",
        ImmutableArray.Create(
            new ParameterSymbol("text", BoundType.NullableAny, 0, NoDefault)
        ),
        NoReturn
    );

    /// <summary>
    /// PrintLine function, writes text to stdout (with line break).
    /// </summary>
    internal static readonly FunctionSymbol PrintLine = new FunctionSymbol(
        "PrintLine",
        ImmutableArray.Create(
            new ParameterSymbol("text", BoundType.NullableAny, 0, NoDefault)
        ),
        NoReturn
    );

    /// <summary>
    /// PrintLine function, writes an empty line to stdout (with line break).
    /// </summary>
    internal static readonly FunctionSymbol PrintLineNoValue = new FunctionSymbol(
        "PrintLine",
        NoParameters,
        NoReturn
    );

    /// <summary>
    /// Input function, gets text input from stdin. Waits until enter is pressed.
    /// </summary>
    internal static readonly FunctionSymbol Input = new FunctionSymbol(
        "Input",
        NoParameters,
        BoundType.String
    );

    /// <summary>
    /// RandInt function, gets a random integer with a maximum (minimum is always 0).
    /// </summary>
    internal static readonly FunctionSymbol RandInt = new FunctionSymbol(
        "RandInt",
        ImmutableArray.Create(
            new ParameterSymbol("max", BoundType.NullableInt, 0, NoDefault)
        ),
        BoundType.Int
    );

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Any type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueAny = new FunctionSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableAny, 0, NoDefault)
        ),
        BoundType.Any
    );

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueBool = new FunctionSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableBool, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueDecimal = new FunctionSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableDecimal, 0, NoDefault)
        ),
        BoundType.Decimal
    );

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// Integer type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueInt = new FunctionSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault)
        ),
        BoundType.Int
    );

    /// <summary>
    /// Value function, gets non nullable value from nullable item (throws if item is null).
    /// String type overload.
    /// </summary>
    internal static readonly FunctionSymbol ValueString = new FunctionSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableString, 0, NoDefault)
        ),
        BoundType.String
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Any type overload.
    /// </summary>
    internal static readonly FunctionSymbol HasValueAny = new FunctionSymbol(
        "HasValue",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableAny, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly FunctionSymbol HasValueBool = new FunctionSymbol(
        "HasValue",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableBool, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly FunctionSymbol HasValueDecimal = new FunctionSymbol(
        "HasValue",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableDecimal, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Int type overload.
    /// </summary>
    internal static readonly FunctionSymbol HasValueInt = new FunctionSymbol(
        "HasValue",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// String type overload.
    /// </summary>
    internal static readonly FunctionSymbol HasValueString = new FunctionSymbol(
        "HasValue",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableString, 0, NoDefault)
        ),
        BoundType.Bool
    );

    private static ImmutableArray<ParameterSymbol> NoParameters {
        get {
            return ImmutableArray<ParameterSymbol>.Empty;
        }
    }

    private static BoundType NoReturn {
        get {
            return new BoundType(TypeSymbol.Void);
        }
    }

    private static BoundExpression NoDefault {
        get {
            return null;
        }
    }

    /// <summary>
    /// Gets all builtin functions.
    /// </summary>
    /// <returns>All builtins, calling code should not depend on order.</returns>
    internal static IEnumerable<FunctionSymbol> GetAll()
        => typeof(BuiltinFunctions).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(FunctionSymbol))
            .Select(f => (FunctionSymbol)f.GetValue(null));
}
