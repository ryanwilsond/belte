using Mono.Cecil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    private static class NetMethodReference {
        internal static MethodReference Object_ctor;
        internal static MethodReference Object_Equals_OO;
        internal static MethodReference Console_Write_S;
        internal static MethodReference Console_Write_O;
        internal static MethodReference Console_WriteLine;
        internal static MethodReference Console_WriteLine_S;
        internal static MethodReference Console_WriteLine_O;
        internal static MethodReference Console_ReadLine;
        internal static MethodReference String_Concat_SS;
        internal static MethodReference String_Concat_SSS;
        internal static MethodReference String_Concat_SSSS;
        internal static MethodReference String_Concat_A;
        internal static MethodReference Convert_ToBoolean_S;
        internal static MethodReference Convert_ToInt64_S;
        internal static MethodReference Convert_ToInt64_D;
        internal static MethodReference Convert_ToDouble_S;
        internal static MethodReference Convert_ToDouble_I;
        internal static MethodReference Convert_ToString_I;
        internal static MethodReference Convert_ToString_D;
        internal static MethodReference Random_ctor;
        internal static MethodReference Random_Next_I;
        internal static MethodReference Nullable_ctor;
        internal static MethodReference Nullable_Value;
        internal static MethodReference Nullable_HasValue;
        internal static MethodReference Type_GetTypeFromHandle;
        internal static MethodReference NullReferenceException_ctor;
    }
}
