using System;
using System.Reflection;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Executor {
    internal static class NetMethodInfo {
        internal static ConstructorInfo Object_ctor = typeof(object).GetConstructor(Type.EmptyTypes);
        internal static MethodInfo Object_Equals_OO;
        internal static MethodInfo Console_Write_S = typeof(Console).GetMethod("Write", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        internal static MethodInfo Console_Write_O = typeof(Console).GetMethod("Write", BindingFlags.Public | BindingFlags.Static, [typeof(object)]);
        internal static MethodInfo Console_WriteLine = typeof(Console).GetMethod("WriteLine", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        internal static MethodInfo Console_WriteLine_S = typeof(Console).GetMethod("WriteLine", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        internal static MethodInfo Console_WriteLine_O = typeof(Console).GetMethod("WriteLine", BindingFlags.Public | BindingFlags.Static, [typeof(object)]);
        internal static MethodInfo Console_ReadLine = typeof(Console).GetMethod("ReadLine", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        internal static MethodInfo String_Concat_SS;
        internal static MethodInfo String_Concat_SSS;
        internal static MethodInfo String_Concat_SSSS;
        internal static MethodInfo String_Concat_A;
        internal static MethodInfo Convert_ToBoolean_S;
        internal static MethodInfo Convert_ToInt64_S;
        internal static MethodInfo Convert_ToInt64_D;
        internal static MethodInfo Convert_ToDouble_S;
        internal static MethodInfo Convert_ToDouble_I;
        internal static MethodInfo Convert_ToString_I;
        internal static MethodInfo Convert_ToString_D;
        internal static MethodInfo Random_ctor;
        internal static MethodInfo Random_Next_I;
        internal static MethodInfo Nullable_ctor;
        internal static MethodInfo Nullable_Value;
        internal static MethodInfo Nullable_HasValue;
        internal static MethodInfo Type_GetTypeFromHandle;
        internal static MethodInfo NullReferenceException_ctor;
    }
}
