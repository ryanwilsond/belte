using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using Buckle.CodeAnalysis;

namespace Buckle.Utilities;

internal static partial class CryptoBlobParser {
    private static readonly ImmutableArray<byte> EcmaKey = [0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0];

    private const byte PublicKeyBlobId = 0x06;
    private const byte PrivateKeyBlobId = 0x07;
    private const int SnPublicKeyBlobSize = 13;
    private const int BlobHeaderSize = sizeof(byte) + sizeof(byte) + sizeof(ushort) + sizeof(uint);
    private const int RsaPubKeySize = sizeof(uint) + sizeof(uint) + sizeof(uint);
    private const int OffsetToKeyData = BlobHeaderSize + RsaPubKeySize;
    private const uint RSA1 = 0x31415352;
    private const uint RSA2 = 0x32415352;

    internal const int PublicKeyHeaderSize = SnPublicKeyBlobSize - 1;

    internal static bool IsValidPublicKey(ImmutableArray<byte> blob) {
        if (blob.IsDefault || blob.Length < PublicKeyHeaderSize + 1)
            return false;

        var blobReader = new LittleEndianReader(blob.AsSpan());

        var sigAlgId = blobReader.ReadUInt32();
        var hashAlgId = blobReader.ReadUInt32();
        var publicKeySize = blobReader.ReadUInt32();
        var publicKey = blobReader.ReadByte();

        if (blob.Length != PublicKeyHeaderSize + publicKeySize)
            return false;

        if (ByteSequenceComparer.Equals(blob, EcmaKey))
            return true;

        if (publicKey != PublicKeyBlobId)
            return false;

        var signatureAlgorithmId = new AlgorithmId(sigAlgId);

        if (signatureAlgorithmId.isSet && signatureAlgorithmId.@class != AlgorithmClass.Signature)
            return false;

        var hashAlgorithmId = new AlgorithmId(hashAlgId);

        if (hashAlgorithmId.isSet &&
            (hashAlgorithmId.@class != AlgorithmClass.Hash || hashAlgorithmId.subId < AlgorithmSubId.Sha1Hash)) {
            return false;
        }

        return true;
    }

    internal static bool TryParseKey(
        ImmutableArray<byte> blob,
        out ImmutableArray<byte> snKey,
        out RSAParameters? privateKey) {
        privateKey = null;
        snKey = default;

        if (IsValidPublicKey(blob)) {
            snKey = blob;
            return true;
        }

        if (blob.Length < BlobHeaderSize + RsaPubKeySize)
            return false;

        try {
            var br = new LittleEndianReader(blob.AsSpan());

            var bType = br.ReadByte();
            var bVersion = br.ReadByte();
            br.ReadUInt16();
            var algId = br.ReadUInt32();
            var magic = br.ReadUInt32();
            var bitLen = br.ReadUInt32();
            var pubExp = br.ReadUInt32();
            var modulusLength = (int)(bitLen / 8);

            if (blob.Length - OffsetToKeyData < modulusLength) {
                return false;
            }

            var modulus = br.ReadBytes(modulusLength);

            if (!(bType == PrivateKeyBlobId && magic == RSA2) && !(bType == PublicKeyBlobId && magic == RSA1)) {
                return false;
            }

            if (bType == PrivateKeyBlobId) {
                privateKey = ToRSAParameters(blob.AsSpan(), true);
                // For snKey, rewrite some of the parameters
                algId = AlgorithmId.RsaSign;
                magic = RSA1;
            }

            snKey = CreateSnPublicKeyBlob(PublicKeyBlobId, bVersion, algId, RSA1, bitLen, pubExp, modulus);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    internal static RSAParameters ToRSAParameters(this ReadOnlySpan<byte> cspBlob, bool includePrivateParameters) {
        var br = new LittleEndianReader(cspBlob);

        var bType = br.ReadByte();
        var bVersion = br.ReadByte();
        br.ReadUInt16();
        var algId = br.ReadInt32();

        var magic = br.ReadInt32();
        var bitLen = br.ReadInt32();

        var modulusLength = bitLen / 8;
        var halfModulusLength = (modulusLength + 1) / 2;

        var expAsDword = br.ReadUInt32();

        var rsaParameters = new RSAParameters {
            Exponent = ExponentAsBytes(expAsDword),
            Modulus = br.ReadReversed(modulusLength)
        };

        if (includePrivateParameters) {
            rsaParameters.P = br.ReadReversed(halfModulusLength);
            rsaParameters.Q = br.ReadReversed(halfModulusLength);
            rsaParameters.DP = br.ReadReversed(halfModulusLength);
            rsaParameters.DQ = br.ReadReversed(halfModulusLength);
            rsaParameters.InverseQ = br.ReadReversed(halfModulusLength);
            rsaParameters.D = br.ReadReversed(modulusLength);
        }

        return rsaParameters;
    }

    private static byte[] ExponentAsBytes(uint exponent) {
        if (exponent <= 0xFF) {
            return [(byte)exponent];
        } else if (exponent <= 0xFFFF) {
            unchecked {
                return [(byte)(exponent >> 8), (byte)exponent];
            }
        } else if (exponent <= 0xFFFFFF) {
            unchecked {
                return [(byte)(exponent >> 16), (byte)(exponent >> 8), (byte)exponent];
            }
        } else {
            return [(byte)(exponent >> 24), (byte)(exponent >> 16), (byte)(exponent >> 8), (byte)exponent];
        }
    }

    private static ImmutableArray<byte> CreateSnPublicKeyBlob(
        byte type,
        byte version,
        uint algId,
        uint magic,
        uint bitLen,
        uint pubExp,
        ReadOnlySpan<byte> pubKeyData) {
        var w = new BlobWriter(3 * sizeof(uint) + OffsetToKeyData + pubKeyData.Length);
        w.WriteUInt32(AlgorithmId.RsaSign);
        w.WriteUInt32(AlgorithmId.Sha);
        w.WriteUInt32((uint)(OffsetToKeyData + pubKeyData.Length));

        w.WriteByte(type);
        w.WriteByte(version);
        w.WriteUInt16(0);
        w.WriteUInt32(algId);

        w.WriteUInt32(magic);
        w.WriteUInt32(bitLen);

        w.WriteUInt32(pubExp);

        unsafe {
            fixed (byte* bytes = pubKeyData)
                w.WriteBytes(bytes, pubKeyData.Length);
        }

        return w.ToImmutableArray();
    }
}
