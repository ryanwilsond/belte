
namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BinderFactory {
    internal enum NodeUsage : byte {
        Normal = 0,

        MethodTypeParameters = 1 << 0,
        MethodBody = 1 << 1,

        ConstructorBodyOrInitializer = 1 << 0,
        OperatorBody = 1 << 0,

        NamedTypeBodyOrTemplateParameters = 1 << 1,
        NamedTypeBase = 1 << 2,

        CompilationUnitScript = 1 << 1,
    }
}
