using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedClosureEnvironmentConstructor : SynthesizedInstanceConstructorSymbol {
    internal SynthesizedClosureEnvironmentConstructor(SynthesizedClosureEnvironment frame) : base(frame) { }
}
