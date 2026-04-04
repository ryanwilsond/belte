using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

public sealed class ParseOptions {
    internal ParseOptions(ImmutableArray<string> preprocessorSymbols) {
        this.preprocessorSymbols = preprocessorSymbols;
    }

    public ImmutableArray<string> preprocessorSymbols { get; }
}
