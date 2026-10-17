
namespace Buckle.CodeAnalysis.Symbols;

internal enum SynthesizedLocalKind : sbyte {
    CompileTimeLowererTemp = -5,
    FlowLowererTemp = -4,
    ExpanderTemp = -3,
    EmitterTemp = -2,
    OptimizerTemp = -1,
    UserDefined = 0,
    LambdaDisplayClass = 1,
    CommitLocal = 2,
}
