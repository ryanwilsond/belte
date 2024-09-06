using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BoundFactory;
using static Buckle.CodeAnalysis.Symbols.SymbolUtilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// All builtin methods used by the compiler with no Standard implementation.
/// </summary>
internal static class BuiltinMethods {
    /// <summary>
    /// RandInt method, gets a random integer with a maximum (minimum is always 0).
    /// </summary>
    internal static readonly MethodSymbol RandInt = new MethodSymbol(
        "RandInt",
        [], [], [new ParameterSymbol("max", BoundType.Int, 0, NoDefault)],
        BoundType.Int
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Any type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueAny = new MethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", BoundType.NullableAny, 0, NoDefault)],
        BoundType.Any
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueBool = new MethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", BoundType.NullableBool, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueDecimal = new MethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", BoundType.NullableDecimal, 0, NoDefault)],
        BoundType.Decimal
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Integer type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueInt = new MethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault)],
        BoundType.Int
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// String type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueString = new MethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", BoundType.NullableString, 0, NoDefault)],
        BoundType.String
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Char type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueChar = new MethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", BoundType.NullableChar, 0, NoDefault)],
        BoundType.Char
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Any type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueAny = new MethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", BoundType.NullableAny, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueBool = new MethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", BoundType.NullableBool, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueDecimal = new MethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", BoundType.NullableDecimal, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Int type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueInt = new MethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// String type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueString = new MethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", BoundType.NullableString, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Char type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueChar = new MethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", BoundType.NullableChar, 0, NoDefault)],
        BoundType.Bool
    );

    /// <summary>
    /// Converts an integer into a base 16 representation.
    /// Optionally adds the '0x' prefix.
    /// </summary>
    internal static readonly MethodSymbol Hex = new MethodSymbol(
        "Hex",
        [], [], [
            new ParameterSymbol("value", BoundType.Int, 0, NoDefault),
            new ParameterSymbol("prefix", BoundType.Bool, 0, Literal(false, BoundType.Bool)),
        ],
        BoundType.String
    );

    /// <summary>
    /// Converts an integer into a base 16 representation.
    /// Optionally adds the '0x' prefix.
    /// </summary>
    internal static readonly MethodSymbol NullableHex = new MethodSymbol(
        "Hex",
        [], [], [
            new ParameterSymbol("value", BoundType.NullableInt, 0, NoDefault),
            new ParameterSymbol("prefix", BoundType.Bool, 0, Literal(false, BoundType.Bool)),
        ],
        BoundType.NullableString
    );

    /// <summary>
    /// Converts a string of length 1 to the appropriate ASCII code of the character.
    /// </summary>
    internal static readonly MethodSymbol Ascii = new MethodSymbol(
        "Ascii",
        [], [], [new ParameterSymbol("char", BoundType.String, 0, NoDefault)],
        BoundType.Int
    );

    /// <summary>
    /// Converts a string of length 1 to the appropriate ASCII code of the character.
    /// </summary>
    internal static readonly MethodSymbol NullableAscii = new MethodSymbol(
        "Ascii",
        [], [], [new ParameterSymbol("char", BoundType.NullableString, 0, NoDefault)],
        BoundType.NullableInt
    );

    /// <summary>
    /// Converts an integer to the appropriate character using ASCII codes.
    /// Opposite of <see cref="Ascii">.
    /// </summary>
    internal static readonly MethodSymbol Char = new MethodSymbol(
        "Char",
        [], [], [new ParameterSymbol("ascii", BoundType.Int, 0, NoDefault)],
        BoundType.String
    );

    /// <summary>
    /// Converts an integer to the appropriate character using ASCII codes.
    /// Opposite of <see cref="Ascii">.
    /// </summary>
    internal static readonly MethodSymbol NullableChar = new MethodSymbol(
        "Char",
        [], [], [new ParameterSymbol("ascii", BoundType.NullableInt, 0, NoDefault)],
        BoundType.NullableString
    );

    /// <summary>
    /// Gets the length of the given array. If given a non-array, returns null.
    /// </summary>
    internal static readonly MethodSymbol Length = new MethodSymbol(
        "Length",
        [], [], [new ParameterSymbol("array", BoundType.NullableAny, 0, NoDefault)],
        BoundType.NullableInt
    );

    /// <summary>
    /// LowLevel only.
    /// Converts a truly generic type into a generic primitive.
    /// </summary>
    internal static readonly MethodSymbol ToAny = new MethodSymbol(
        "ToAny",
        [], [], [new ParameterSymbol("primitive", BoundType.NullableAny, 0, NoDefault)],
        BoundType.NullableAny
    );

    /// <summary>
    /// LowLevel only.
    /// Converts a truly generic type into a generic object.
    /// </summary>
    internal static readonly MethodSymbol ToObject = new MethodSymbol(
        "ToObject",
        [], [], [new ParameterSymbol("object", BoundType.NullableAny, 0, NoDefault)],
        BoundType.NullableAny
    );

    /// <summary>
    /// Gets all public builtin methods.
    /// </summary>
    /// <returns>All public builtins, calling code should not depend on order.</returns>
    internal static IEnumerable<MethodSymbol> GetAll()
        => [RandInt, Hex, NullableHex, Ascii, NullableAscii, Char, NullableChar, Length, ToAny, ToObject];
}
