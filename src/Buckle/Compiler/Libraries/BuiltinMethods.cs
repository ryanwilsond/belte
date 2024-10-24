using System.Collections.Generic;
using Buckle.Libraries.Standard;
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
    internal static readonly MethodSymbol RandInt = new SynthesizedMethodSymbol(
        "RandInt",
        [], [], [new ParameterSymbol("max", TypeWithAnnotations.Int, 0, NoDefault)],
        TypeWithAnnotations.Int
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Any type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueAny = new SynthesizedMethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableAny, 0, NoDefault)],
        TypeWithAnnotations.Any
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueBool = new SynthesizedMethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableBool, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueDecimal = new SynthesizedMethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableDecimal, 0, NoDefault)],
        TypeWithAnnotations.Decimal
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Integer type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueInt = new SynthesizedMethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableInt, 0, NoDefault)],
        TypeWithAnnotations.Int
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// String type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueString = new SynthesizedMethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableString, 0, NoDefault)],
        TypeWithAnnotations.String
    );

    /// <summary>
    /// Value method, gets non nullable value from nullable item (throws if item is null).
    /// Char type overload.
    /// </summary>
    internal static readonly MethodSymbol ValueChar = new SynthesizedMethodSymbol(
        "Value",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableChar, 0, NoDefault)],
        TypeWithAnnotations.Char
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Any type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueAny = new SynthesizedMethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableAny, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Bool type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueBool = new SynthesizedMethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableBool, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Decimal type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueDecimal = new SynthesizedMethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableDecimal, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Int type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueInt = new SynthesizedMethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableInt, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// String type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueString = new SynthesizedMethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableString, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Checks if nullable item has a value (otherwise it is null).
    /// Char type overload.
    /// </summary>
    internal static readonly MethodSymbol HasValueChar = new SynthesizedMethodSymbol(
        "HasValue",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableChar, 0, NoDefault)],
        TypeWithAnnotations.Bool
    );

    /// <summary>
    /// Converts an integer into a base 16 representation.
    /// Optionally adds the '0x' prefix.
    /// </summary>
    internal static readonly MethodSymbol Hex = new SynthesizedMethodSymbol(
        "Hex",
        [], [], [
            new ParameterSymbol("value", TypeWithAnnotations.Int, 0, NoDefault),
            new ParameterSymbol("prefix", TypeWithAnnotations.Bool, 1, Literal(false)),
        ],
        TypeWithAnnotations.String
    );

    /// <summary>
    /// Converts an integer into a base 16 representation.
    /// Optionally adds the '0x' prefix.
    /// </summary>
    internal static readonly MethodSymbol NullableHex = new SynthesizedMethodSymbol(
        "Hex",
        [], [], [
            new ParameterSymbol("value", TypeWithAnnotations.NullableInt, 0, NoDefault),
            new ParameterSymbol("prefix", TypeWithAnnotations.Bool, 1, Literal(false)),
        ],
        TypeWithAnnotations.NullableString
    );

    /// <summary>
    /// Converts a string of length 1 to the appropriate ASCII code of the character.
    /// </summary>
    internal static readonly MethodSymbol Ascii = new SynthesizedMethodSymbol(
        "Ascii",
        [], [], [new ParameterSymbol("char", TypeWithAnnotations.String, 0, NoDefault)],
        TypeWithAnnotations.Int
    );

    /// <summary>
    /// Converts a string of length 1 to the appropriate ASCII code of the character.
    /// </summary>
    internal static readonly MethodSymbol NullableAscii = new SynthesizedMethodSymbol(
        "Ascii",
        [], [], [new ParameterSymbol("char", TypeWithAnnotations.NullableString, 0, NoDefault)],
        TypeWithAnnotations.NullableInt
    );

    /// <summary>
    /// Converts an integer to the appropriate character using ASCII codes.
    /// Opposite of <see cref="Ascii">.
    /// </summary>
    internal static readonly MethodSymbol Char = new SynthesizedMethodSymbol(
        "Char",
        [], [], [new ParameterSymbol("ascii", TypeWithAnnotations.Int, 0, NoDefault)],
        TypeWithAnnotations.String
    );

    /// <summary>
    /// Converts an integer to the appropriate character using ASCII codes.
    /// Opposite of <see cref="Ascii">.
    /// </summary>
    internal static readonly MethodSymbol NullableChar = new SynthesizedMethodSymbol(
        "Char",
        [], [], [new ParameterSymbol("ascii", TypeWithAnnotations.NullableInt, 0, NoDefault)],
        TypeWithAnnotations.NullableString
    );

    /// <summary>
    /// Gets the length of the given array. If given a non-array, returns null.
    /// </summary>
    internal static readonly MethodSymbol LengthNull = new SynthesizedMethodSymbol(
        "Length",
        [], [], [new ParameterSymbol("array", TypeWithAnnotations.NullableAny, 0, NoDefault)],
        TypeWithAnnotations.NullableInt
    );

    /// <summary>
    /// Gets the length of the given array.
    /// </summary>
    internal static readonly MethodSymbol Length = new SynthesizedMethodSymbol(
        "Length",
        [], [], [new ParameterSymbol("array", TypeWithAnnotations.Any, 0, NoDefault)],
        TypeWithAnnotations.Int
    );

    /// <summary>
    /// LowLevel only.
    /// Converts a truly generic type into a generic primitive.
    /// </summary>
    internal static readonly MethodSymbol ToAny = new SynthesizedMethodSymbol(
        "ToAny",
        [], [], [new ParameterSymbol("primitive", TypeWithAnnotations.NullableAny, 0, NoDefault)],
        TypeWithAnnotations.NullableAny
    );

    /// <summary>
    /// LowLevel only.
    /// Converts a truly generic type into a generic object.
    /// </summary>
    internal static readonly MethodSymbol ToObject = new SynthesizedMethodSymbol(
        "ToObject",
        [], [], [new ParameterSymbol("object", TypeWithAnnotations.NullableAny, 0, NoDefault)],
        TypeWithAnnotations.NullableAny
    );

    /// <summary>
    /// LowLevel only.
    /// Checks if two objects values equal.
    /// </summary>
    internal static readonly MethodSymbol ObjectsEqual = new SynthesizedMethodSymbol(
        "ObjectsEqual",
        [], [], [
            new ParameterSymbol("x", new TypeWithAnnotations(StandardLibrary.Object, true), 0, NoDefault),
            new ParameterSymbol("y", new TypeWithAnnotations(StandardLibrary.Object, true), 1, NoDefault)
        ],
        TypeWithAnnotations.NullableBool
    );

    /// <summary>
    /// LowLevel only.
    /// Checks if two references refer to the same object.
    /// </summary>
    internal static readonly MethodSymbol ObjectReferencesEqual = new SynthesizedMethodSymbol(
        "ObjectReferencesEqual",
        [], [], [
            new ParameterSymbol(
                "x",
                new TypeWithAnnotations(StandardLibrary.Object, true),
                0,
                NoDefault,
                DeclarationModifiers.Ref
            ),
            new ParameterSymbol(
                "y",
                new TypeWithAnnotations(StandardLibrary.Object, true),
                1,
                NoDefault,
                DeclarationModifiers.Ref
            )
        ],
        TypeWithAnnotations.NullableBool
    );

    /// <summary>
    /// Gets the hash of a primitive or object.
    /// </summary>
    internal static readonly new MethodSymbol GetHashCode = new SynthesizedMethodSymbol(
        "GetHashCode",
        [], [], [new ParameterSymbol("value", TypeWithAnnotations.NullableAny, 0, NoDefault)],
        TypeWithAnnotations.Int
    );

    /// <summary>
    /// Gets all public builtin methods.
    /// </summary>
    /// <returns>All public builtins, calling code should not depend on order.</returns>
    internal static IEnumerable<MethodSymbol> GetAll()
        => [
            RandInt,
            Hex,
            NullableHex,
            Ascii,
            NullableAscii,
            Char,
            NullableChar,
            LengthNull,
            Length,
            ToAny,
            ToObject,
            ObjectsEqual,
            ObjectReferencesEqual,
            GetHashCode
        ];
}
