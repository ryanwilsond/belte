using System;
using Mono.Cecil;

namespace Buckle.CodeAnalysis.Emitting;

internal static class ThreadSafeExtensions {
    internal static TypeReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        TypeReference typeReference) {
        lock (ILEmitter.GlobalCecilLock)
            return module.ImportReference(typeReference);
    }

    internal static TypeReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        TypeReference typeReference,
        IGenericParameterProvider context) {
        lock (ILEmitter.GlobalCecilLock)
            return module.ImportReference(typeReference, context);
    }

    internal static MethodReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        MethodReference methodReference) {
        lock (ILEmitter.GlobalCecilLock)
            return module.ImportReference(methodReference);
    }

    internal static MethodReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        System.Reflection.MethodBase methodReference) {
        lock (ILEmitter.GlobalCecilLock)
            return module.ImportReference(methodReference);
    }

    internal static TypeReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        Type type) {
        lock (ILEmitter.GlobalCecilLock)
            return module.ImportReference(type);
    }
}
