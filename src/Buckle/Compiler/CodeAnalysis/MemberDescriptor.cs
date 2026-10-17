using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal readonly struct MemberDescriptor {
    internal readonly MemberFlags flags;
    internal readonly ushort arity;
    internal readonly string name;

    private readonly short _declaringTypeId;

    internal MemberDescriptor(
        MemberFlags flags,
        short declaringTypeId,
        string name,
        ImmutableArray<byte> signature,
        ushort arity = 0) {
        this.flags = flags;
        _declaringTypeId = declaringTypeId;
        this.name = name;
        this.arity = arity;
        this.signature = signature;
    }

    internal bool isSpecialTypeMember => _declaringTypeId < (int)SpecialType.NextAvailable;

    internal SpecialType declaringSpecialType {
        get {
            Debug.Assert(_declaringTypeId < (int)SpecialType.NextAvailable);
            return (SpecialType)_declaringTypeId;
        }
    }

    internal WellKnownType declaringWellKnownType {
        get {
            Debug.Assert(_declaringTypeId >= (int)WellKnownType.First);
            return (WellKnownType)_declaringTypeId;
        }
    }

    internal readonly ImmutableArray<byte> signature;

    internal int parametersCount {
        get {
            var memberKind = flags & MemberFlags.KindMask;

            switch (memberKind) {
                case MemberFlags.Constructor:
                case MemberFlags.Method:
                case MemberFlags.PropertyGet:
                case MemberFlags.Property:
                    return signature[0];
                default:
                    throw ExceptionUtilities.UnexpectedValue(memberKind);
            }
        }
    }

    internal static ImmutableArray<MemberDescriptor> InitializeFromStream(Stream stream, string[] nameTable) {
        var count = nameTable.Length;

        var builder = ImmutableArray.CreateBuilder<MemberDescriptor>(count);
        var signatureBuilder = ImmutableArray.CreateBuilder<byte>();

        for (var i = 0; i < count; i++) {
            var flags = (MemberFlags)stream.ReadByte();
            var declaringTypeId = ReadTypeId(stream);
            var arity = (ushort)stream.ReadByte();

            if ((flags & MemberFlags.Field) != 0)
                ParseType(signatureBuilder, stream);
            else
                ParseMethodOrPropertySignature(signatureBuilder, stream);

            builder.Add(new MemberDescriptor(
                flags,
                declaringTypeId,
                nameTable[i],
                signatureBuilder.ToImmutable(),
                arity
            ));

            signatureBuilder.Clear();
        }

        return builder.ToImmutable();
    }

    private static short ReadTypeId(Stream stream) {
        var firstByte = (byte)stream.ReadByte();

        if (firstByte == (byte)WellKnownType.ExtSentinel)
            return (short)(stream.ReadByte() + (byte)WellKnownType.ExtSentinel);
        else
            return firstByte;
    }

    private static void ParseMethodOrPropertySignature(ImmutableArray<byte>.Builder builder, Stream stream) {
        var paramCount = stream.ReadByte();
        builder.Add((byte)paramCount);

        ParseType(builder, stream, allowByRef: true);

        for (var i = 0; i < paramCount; i++)
            ParseType(builder, stream, allowByRef: true);
    }

    private static void ParseType(ImmutableArray<byte>.Builder builder, Stream stream, bool allowByRef = false) {
        while (true) {
            var typeCode = (SignatureTypeCode)stream.ReadByte();
            builder.Add((byte)typeCode);

            switch (typeCode) {
                default:
                    throw ExceptionUtilities.UnexpectedValue(typeCode);
                case SignatureTypeCode.TypeHandle:
                    ParseTypeHandle(builder, stream);
                    return;
                case SignatureTypeCode.GenericTypeParameter:
                case SignatureTypeCode.GenericMethodParameter:
                    builder.Add((byte)stream.ReadByte());
                    return;
                case SignatureTypeCode.ByReference:
                    if (!allowByRef)
                        goto default;

                    break;
                case SignatureTypeCode.SZArray:
                    break;
                case SignatureTypeCode.Pointer:
                    break;
                case SignatureTypeCode.GenericTypeInstance:
                    ParseGenericTypeInstance(builder, stream);
                    return;
            }

            allowByRef = false;
        }
    }

    private static void ParseTypeHandle(ImmutableArray<byte>.Builder builder, Stream stream) {
        var firstByte = (byte)stream.ReadByte();
        builder.Add(firstByte);

        if (firstByte == (byte)WellKnownType.ExtSentinel) {
            var secondByte = (byte)stream.ReadByte();
            builder.Add(secondByte);
        }
    }

    private static void ParseGenericTypeInstance(ImmutableArray<byte>.Builder builder, Stream stream) {
        ParseType(builder, stream);

        var argumentCount = stream.ReadByte();
        builder.Add((byte)argumentCount);

        for (var i = 0; i < argumentCount; i++)
            ParseType(builder, stream);
    }
}
