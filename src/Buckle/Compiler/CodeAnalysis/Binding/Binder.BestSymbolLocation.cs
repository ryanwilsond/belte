using System;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    [Flags]
    private enum BestSymbolLocation : byte {
        None,
        FromSourceModule,
        FromAddedModule,
        FromCorLibrary,
    }
}
