using System;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    [AttributeUsage(AttributeTargets.All)]
    private sealed class BelteCompilerGeneratedAttribute : Attribute { }
}
