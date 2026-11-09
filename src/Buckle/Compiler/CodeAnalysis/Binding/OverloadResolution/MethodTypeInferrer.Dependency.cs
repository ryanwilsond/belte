
namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    private enum Dependency {
        Unknown = 0x00,
        NotDependent = 0x01,
        DependsMask = 0x10,
        Direct = 0x11,
        Indirect = 0x12
    }
}
