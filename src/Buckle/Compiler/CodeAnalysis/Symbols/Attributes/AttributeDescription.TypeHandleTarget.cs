
namespace Buckle.CodeAnalysis.Symbols;

internal partial struct AttributeDescription {
    internal enum TypeHandleTarget : byte {
        AttributeTargets,
        AssemblyNameFlags,
        MethodImplOptions,
        CharSet,
        LayoutKind,
        UnmanagedType,
        TypeLibTypeFlags,
        ClassInterfaceType,
        ComInterfaceType,
        CompilationRelaxations,
        DebuggingModes,
        SecurityCriticalScope,
        CallingConvention,
        AssemblyHashAlgorithm,
        TransactionOption,
        SecurityAction,
        SystemType,
        DeprecationType,
        Platform
    }
}
