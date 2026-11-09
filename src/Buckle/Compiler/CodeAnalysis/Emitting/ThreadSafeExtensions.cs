using System;
using System.Diagnostics;
using Mono.Cecil;

namespace Buckle.CodeAnalysis.Emitting;

internal static class ThreadSafeExtensions {
    internal static TypeReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        TypeReference typeReference) {
        if (!ILEmitter.Imports.Add(typeReference))
            Debug.Print($"Type already imported: {typeReference}");

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

    internal static FieldReference ImportReferenceThreadSafe(
        this ModuleDefinition module,
        System.Reflection.FieldInfo fieldInfo) {
        lock (ILEmitter.GlobalCecilLock)
            return module.ImportReference(fieldInfo);
    }
}
