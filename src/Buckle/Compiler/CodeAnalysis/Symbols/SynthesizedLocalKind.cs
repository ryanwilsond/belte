
namespace Buckle.CodeAnalysis.Symbols;

internal enum SynthesizedLocalKind : byte {
    LambdaDisplayClass,
    UserDefined,
    ExpanderTemp,
    EmitterTemp,
}
