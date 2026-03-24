using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Buckle.CodeAnalysis.Evaluating;

internal static class FunctionPointerExtensions {
    internal static Type MakeGenericManagedCallFunctionPointerType(this (Type returnType, Type[] argumentTypes) types) {
        if (!(GetGenericMethod(types.argumentTypes.Length + 1) is { } genericMethod))
            throw new ArgumentException($"Generic arguments of length {types.argumentTypes.Length} are not supported");

        return (Type)genericMethod.MakeGenericMethod(types.argumentTypes.Concat(new[] { types.returnType }).ToArray())
            .Invoke(null, null);
    }

    internal static Type MakeGenericManagedCallVoidFunctionPointerType(this Type[] argumentTypes) {
        if (argumentTypes.Length == 0)
            return typeof(delegate* managed<void>);

        if (!(GetGenericMethod(argumentTypes.Length) is { } genericMethod))
            throw new ArgumentException($"Generic arguments of length {argumentTypes.Length} are not supported");

        return (Type)genericMethod.MakeGenericMethod(argumentTypes).Invoke(null, null);
    }

    static MethodInfo GetGenericMethod(int length, [CallerMemberName] string memberName = "") {
        return typeof(FunctionPointerExtensions).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.Name == memberName && m.IsGenericMethod && m.GetGenericArguments().Length == length)
            .SingleOrDefault();
    }

    static Type MakeGenericManagedCallFunctionPointerType<TReturn>() => typeof(delegate* managed<TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, TReturn>() => typeof(delegate* managed<T1, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, TReturn>() => typeof(delegate* managed<T1, T1, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, TReturn>() => typeof(delegate* managed<T1, T1, T3, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TReturn>);
    static Type MakeGenericManagedCallFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TReturn>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TReturn>);

    static Type MakeGenericManagedCallVoidFunctionPointerType<T1>() => typeof(delegate* managed<T1, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2>() => typeof(delegate* managed<T1, T1, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3>() => typeof(delegate* managed<T1, T1, T3, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4>() => typeof(delegate* managed<T1, T1, T3, T4, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5>() => typeof(delegate* managed<T1, T1, T3, T4, T5, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, void>);
    static Type MakeGenericManagedCallVoidFunctionPointerType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>() => typeof(delegate* managed<T1, T1, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, void>);
}
