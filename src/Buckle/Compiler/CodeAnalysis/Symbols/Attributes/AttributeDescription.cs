using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal struct AttributeDescription {
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

    private static readonly byte[] Signature_HasThis_Void_String = new byte[] { (byte)SignatureAttributes.Instance, 1, Void, String };

    private static readonly byte[][] Signatures_HasThis_Void_String_Only = { Signature_HasThis_Void_String };
}
