using System;
using System.Linq;
using System.Reflection;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor {
    internal static class MethodInfoCache {
        internal static readonly BindingFlags DefaultFlags = BindingFlags.Public | BindingFlags.Static;
        internal static readonly BindingFlags InstFlags = BindingFlags.Public | BindingFlags.Instance;

        internal static ConstructorInfo Object_ctor = typeof(object).GetConstructor(Type.EmptyTypes);
        internal static ConstructorInfo Nullable_ctor = typeof(Nullable<>).GetConstructors().Single();
        internal static ConstructorInfo Exception_ctor = typeof(Exception).GetConstructor(Type.EmptyTypes);
        internal static ConstructorInfo Exception_ctor_S = typeof(Exception).GetConstructor([typeof(string)]);
        internal static MethodInfo Object_ToString = typeof(object).GetMethod("ToString", InstFlags, Type.EmptyTypes);
        internal static MethodInfo String_Concat_SS = typeof(string).GetMethod("Concat", DefaultFlags, [typeof(string), typeof(string)]);
        internal static MethodInfo String_Equality_SS = typeof(string).GetMethod("op_Equality", DefaultFlags, [typeof(string), typeof(string)]);
        internal static MethodInfo String_get_Chars_I = typeof(string).GetMethod("get_Chars", InstFlags, [typeof(int)]);
        internal static ConstructorInfo NullConditionException_ctor = typeof(NullConditionException).GetConstructor(Type.EmptyTypes);
        internal static MethodInfo Type_GetTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", DefaultFlags, [typeof(RuntimeTypeHandle)]);
    }
}
