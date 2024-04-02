using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// All builtin methods (included by default).
/// </summary>
internal static class BuiltinMethods {
    /// <summary>
    /// Print method, writes text to stdout (no line break).
    /// </summary>
    internal static readonly MethodSymbol Print = new MethodSymbol(
        "Print",
        ImmutableArray.Create(
            new ParameterSymbol("text", BoundType.NullableAny, 0, NoDefault)
        ),
        NoReturn
    );

    /// <summary>
    /// PrintLine method, writes text to stdout (with line break).
    /// </summary>
    internal static readonly MethodSymbol PrintLine = new MethodSymbol(
        "PrintLine",
        ImmutableArray.Create(
            new ParameterSymbol("text", BoundType.NullableAny, 0, NoDefault)
        ),
        NoReturn
    );

    /// <summary>
    /// PrintLine method, writes an empty line to stdout (with line break).
    /// </summary>
    internal static readonly MethodSymbol PrintLineNoValue = new MethodSymbol(
        "PrintLine",
        NoParameters,
        NoReturn
    );

    /// <summary>
    /// Input method, gets text input from stdin. Waits until enter is pressed.
    /// </summary>
    internal static readonly MethodSymbol Input = new MethodSymbol(
        "Input",
        NoParameters,
        BoundType.String
    );

    /// <summary>
    /// RandInt method, gets a random integer with a maximum (minimum is always 0).
    /// </summary>
    internal static readonly MethodSymbol RandInt = new MethodSymbol(
        "RandInt",
        ImmutableArray.Create(
            new ParameterSymbol("max", BoundType.NullableInt, 0, NoDefault)
        ),
        BoundType.Int
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Any type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueAny = new MethodSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableAny, 0, NoDefault)
        ),
        BoundType.Any
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueBool = new MethodSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableBool, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueDecimal = new MethodSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableDecimal, 0, NoDefault)
        ),
        BoundType.Decimal
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Integer type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueInt = new MethodSymbol(
        "Value",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault)
        ),
        BoundType.Int
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// String type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueString = new MethodSymbol(
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
    internal static readonly MethodSymbol HasValueAny = new MethodSymbol(
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
    internal static readonly MethodSymbol HasValueBool = new MethodSymbol(
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
    internal static readonly MethodSymbol HasValueDecimal = new MethodSymbol(
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
    internal static readonly MethodSymbol HasValueInt = new MethodSymbol(
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
    internal static readonly MethodSymbol HasValueString = new MethodSymbol(
        "HasValue",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableString, 0, NoDefault)
        ),
        BoundType.Bool
    );

    /// <summary>
    /// Converts an integer into a base 16 representation.
    /// Optionally adds the '0x' prefix.
    /// </summary>
    internal static readonly MethodSymbol Hex = new MethodSymbol(
        "Hex",
        ImmutableArray.Create(
            new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault),
            new ParameterSymbol("prefix", BoundType.Bool, 0, Literal(false, BoundType.Bool))
        ),
        BoundType.String
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
    /// Gets all builtin methods.
    /// </summary>
    /// <returns>All builtins, calling code should not depend on order.</returns>
    internal static IEnumerable<MethodSymbol> GetAll()
        => typeof(BuiltinMethods).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(MethodSymbol))
            .Select(f => (MethodSymbol)f.GetValue(null));
}
