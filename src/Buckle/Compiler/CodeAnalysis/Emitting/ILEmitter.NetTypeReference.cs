using Mono.Cecil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    private static class NetTypeReference {
        internal static TypeReference Random;
        internal static TypeReference Nullable;
        internal static TypeReference ValueType;
    }
}
