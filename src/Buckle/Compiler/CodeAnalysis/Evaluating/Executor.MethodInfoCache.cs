using System;
using System.Reflection;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor {
    internal static class MethodInfoCache {
        internal static ConstructorInfo Object_ctor = typeof(object).GetConstructor(Type.EmptyTypes);
        internal static MethodInfo String_Concat_SS = typeof(string).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(string)]);
        internal static MethodInfo String_Equality_SS = typeof(string).GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(string)]);
    }
}
