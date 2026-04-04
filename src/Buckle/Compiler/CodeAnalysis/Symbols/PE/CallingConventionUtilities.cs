using System.Reflection.Metadata;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class CallingConventionUtilities {
    internal static CallingConvention FromSignatureConvention(SignatureCallingConvention callingConvention) {
        return callingConvention switch {
            SignatureCallingConvention.Default => CallingConvention.Default,
            SignatureCallingConvention.CDecl => CallingConvention.Cdecl,
            SignatureCallingConvention.FastCall => CallingConvention.FastCall,
            SignatureCallingConvention.StdCall => CallingConvention.StdCall,
            SignatureCallingConvention.ThisCall => CallingConvention.ThisCall,
            SignatureCallingConvention.Unmanaged => CallingConvention.Unmanaged,
            // SignatureCallingConvention.VarArgs => CallingConvention.Varargs,
            _ => throw new UnsupportedSignatureContent()
        };
    }
}
