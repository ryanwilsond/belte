using Mono.Cecil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    internal static class NetTypeReference {
        internal static TypeReference Random;
        internal static TypeReference Nullable;
        internal static TypeReference ValueType;
        internal static TypeReference Enum;
        internal static TypeReference Func;
    }
}
