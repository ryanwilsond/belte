
namespace Buckle.CodeAnalysis.Binding;

internal enum UserDefinedConversionResultKind : byte {
    NoApplicableOperators,
    NoBestSourceType,
    NoBestTargetType,
    Ambiguous,
    Valid
}
