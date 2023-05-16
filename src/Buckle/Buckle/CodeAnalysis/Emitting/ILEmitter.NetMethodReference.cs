
namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    private enum NetMethodReference {
        ConsoleWrite,
        ConsoleWriteLine,
        ConsoleWriteLineNoArgs,
        ConsoleReadLine,
        StringConcat2,
        StringConcat3,
        StringConcat4,
        StringConcatArray,
        ConvertToBoolean,
        ConvertToInt32,
        ConvertToString,
        ConvertToDouble,
        ObjectEquals,
        RandomNext,
        RandomCtor,
        NullableCtor,
        NullableValue,
        NullableHasValue,
    }
}
