using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class LocalInProgressBinder : Binder {
    internal LocalInProgressBinder(DataContainerSymbol inProgress, Binder next)
        : base(next) {
        localInProgress = inProgress;
    }

    internal override DataContainerSymbol localInProgress { get; }
}
