using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal partial struct AttributeDescription {
    internal static ImmutableArray<TypeHandleTargetInfo> TypeHandleTargets;

    internal readonly string @namespace;
    internal readonly string name;
    internal readonly byte[][] signatures;

    internal readonly bool matchIgnoringCase;

    internal AttributeDescription(string @namespace, string name, byte[][] signatures, bool matchIgnoringCase = false) {
        this.@namespace = @namespace;
        this.name = name;
        this.signatures = signatures;
        this.matchIgnoringCase = matchIgnoringCase;
    }

    internal string fullName => @namespace + "." + name;

    internal int GetParameterCount(int signatureIndex) {
        return signatures[signatureIndex][1];
    }

    public override string ToString() {
        return fullName + "(" + signatures.Length + ")";
    }

    private const byte Void = (byte)SignatureTypeCode.Void;
    private const byte Boolean = (byte)SignatureTypeCode.Boolean;
    private const byte Byte = (byte)SignatureTypeCode.Byte;
    private const byte Int16 = (byte)SignatureTypeCode.Int16;
    private const byte Int32 = (byte)SignatureTypeCode.Int32;
    private const byte UInt32 = (byte)SignatureTypeCode.UInt32;
    private const byte Int64 = (byte)SignatureTypeCode.Int64;
    private const byte String = (byte)SignatureTypeCode.String;
    private const byte Object = (byte)SignatureTypeCode.Object;
    private const byte SzArray = (byte)SignatureTypeCode.SZArray;
    private const byte TypeHandle = (byte)SignatureTypeCode.TypeHandle;

    private static readonly byte[] Signature_HasThis_Void = [(byte)SignatureAttributes.Instance, 0, Void];
    private static readonly byte[] Signature_HasThis_Void_Boolean = [(byte)SignatureAttributes.Instance, 1, Void, Boolean];
    private static readonly byte[] Signature_HasThis_Void_String = [(byte)SignatureAttributes.Instance, 1, Void, String];
    private static readonly byte[] Signature_HasThis_Void_String_String = [(byte)SignatureAttributes.Instance, 2, Void, String, String];
    private static readonly byte[] Signature_HasThis_Void_Int32 = [(byte)SignatureAttributes.Instance, 1, Void, Int32];
    private static readonly byte[] Signature_HasThis_Void_CompilationRelaxations = [(byte)SignatureAttributes.Instance, 1, Void, TypeHandle, (byte)TypeHandleTarget.CompilationRelaxations];

    private static readonly byte[][] Signatures_HasThis_Void_Only = [Signature_HasThis_Void];
    private static readonly byte[][] Signatures_HasThis_Void_String_Only = [Signature_HasThis_Void_String];
    private static readonly byte[][] Signatures_HasThis_Void_Boolean_Only = [Signature_HasThis_Void_Boolean];
    private static readonly byte[][] Signatures_HasThis_Void_Int32_Only = [Signature_HasThis_Void_Int32];

    private static readonly byte[][] SignaturesOfTypeIdentifierAttribute = [Signature_HasThis_Void, Signature_HasThis_Void_String_String];
    private static readonly byte[][] SignaturesOfCompilationRelaxationsAttribute = [Signature_HasThis_Void_Int32, Signature_HasThis_Void_CompilationRelaxations];

    internal static readonly AttributeDescription InternalsVisibleToAttribute = new AttributeDescription("System.Runtime.CompilerServices", "InternalsVisibleToAttribute", Signatures_HasThis_Void_String_Only);
    internal static readonly AttributeDescription TypeIdentifierAttribute = new AttributeDescription("System.Runtime.InteropServices", "TypeIdentifierAttribute", SignaturesOfTypeIdentifierAttribute);
    internal static readonly AttributeDescription ParamArrayAttribute = new AttributeDescription("System", "ParamArrayAttribute", Signatures_HasThis_Void_Only);
    internal static readonly AttributeDescription CaseSensitiveExtensionAttribute = new AttributeDescription("System.Runtime.CompilerServices", "ExtensionAttribute", Signatures_HasThis_Void_Only, matchIgnoringCase: false);
    internal static readonly AttributeDescription IsReadOnlyAttribute = new AttributeDescription("System.Runtime.CompilerServices", "IsReadOnlyAttribute", Signatures_HasThis_Void_Only);
    internal static readonly AttributeDescription CompilationRelaxationsAttribute = new AttributeDescription("System.Runtime.CompilerServices", "CompilationRelaxationsAttribute", SignaturesOfCompilationRelaxationsAttribute);
    internal static readonly AttributeDescription RuntimeCompatibilityAttribute = new AttributeDescription("System.Runtime.CompilerServices", "RuntimeCompatibilityAttribute", Signatures_HasThis_Void_Only);
    internal static readonly AttributeDescription RefSafetyRulesAttribute = new AttributeDescription("System.Runtime.CompilerServices", "RefSafetyRulesAttribute", Signatures_HasThis_Void_Int32_Only);
    internal static readonly AttributeDescription NullablePublicOnlyAttribute = new AttributeDescription("System.Runtime.CompilerServices", "NullablePublicOnlyAttribute", Signatures_HasThis_Void_Boolean_Only);
    internal static readonly AttributeDescription GuidAttribute = new AttributeDescription("System.Runtime.InteropServices", "GuidAttribute", Signatures_HasThis_Void_String_Only);
}
