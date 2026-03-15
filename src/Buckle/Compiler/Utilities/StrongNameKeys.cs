using System;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.Diagnostics;

namespace Buckle.Utilities;

internal sealed class StrongNameKeys {
    private static Tuple<ImmutableArray<byte>, ImmutableArray<byte>, RSAParameters?> LastSeenKeyPair;

    internal readonly ImmutableArray<byte> keyPair;
    internal readonly ImmutableArray<byte> publicKey;
    internal readonly RSAParameters? privateKey;
    internal readonly BelteDiagnostic diagnosticOpt;
    internal readonly string? keyContainer;
    internal readonly string? keyFilePath;
    internal readonly bool hasCounterSignature;

    internal static readonly StrongNameKeys None = new StrongNameKeys();

    private StrongNameKeys() { }

    internal StrongNameKeys(BelteDiagnostic diagnostic) {
        diagnosticOpt = diagnostic;
    }

    internal StrongNameKeys(
        ImmutableArray<byte> keyPair,
        ImmutableArray<byte> publicKey,
        RSAParameters? privateKey,
        string keyContainerName,
        string keyFilePath,
        bool hasCounterSignature) {
        this.keyPair = keyPair;
        this.publicKey = publicKey;
        this.privateKey = privateKey;
        keyContainer = keyContainerName;
        this.keyFilePath = keyFilePath;
        this.hasCounterSignature = hasCounterSignature;
    }

    internal static StrongNameKeys Create(
        ImmutableArray<byte> publicKey,
        RSAParameters privateKey,
        bool hasCounterSignature) {

        if (MetadataHelpers.IsValidPublicKey(publicKey)) {
            return new StrongNameKeys(
                keyPair: default,
                publicKey,
                privateKey,
                keyContainerName: null,
                keyFilePath: null,
                hasCounterSignature
            );
        } else {
            // TODO error
            // return new StrongNameKeys(messageProvider.CreateDiagnostic(messageProvider.ERR_BadCompilationOptionValue, Location.None,
            //     nameof(CompilationOptions.CryptoPublicKey), BitConverter.ToString(publicKey.ToArray())));
            return new StrongNameKeys(null);
        }
    }

    internal static StrongNameKeys Create(string keyFilePath) {
        if (string.IsNullOrEmpty(keyFilePath))
            return None;

        try {
            var fileContent = ImmutableArray.Create(File.ReadAllBytes(keyFilePath));
            return CreateHelper(fileContent, keyFilePath, hasCounterSignature: false);
        } catch (IOException ex) {
            return new StrongNameKeys(GetKeyFileError(keyFilePath, ex.Message));
        }
    }

    internal static StrongNameKeys CreateHelper(
        ImmutableArray<byte> keyFileContent,
        string keyFilePath,
        bool hasCounterSignature) {
        ImmutableArray<byte> keyPair;
        ImmutableArray<byte> publicKey;
        RSAParameters? privateKey = null;

        var cachedKeyPair = LastSeenKeyPair;

        if (cachedKeyPair is not null && keyFileContent == cachedKeyPair.Item1) {
            keyPair = cachedKeyPair.Item1;
            publicKey = cachedKeyPair.Item2;
            privateKey = cachedKeyPair.Item3;
        } else {
            if (MetadataHelpers.IsValidPublicKey(keyFileContent)) {
                publicKey = keyFileContent;
                keyPair = default;
            } else if (CryptoBlobParser.TryParseKey(keyFileContent, out publicKey, out privateKey)) {
                keyPair = keyFileContent;
            } else {
                // TODO message
                // throw new IOException(CodeAnalysisResources.InvalidPublicKey);
                throw new IOException();
            }

            cachedKeyPair = new Tuple<ImmutableArray<byte>, ImmutableArray<byte>, RSAParameters?>(keyPair, publicKey, privateKey);
            Interlocked.Exchange(ref LastSeenKeyPair, cachedKeyPair);
        }

        return new StrongNameKeys(keyPair, publicKey, privateKey, null, keyFilePath, hasCounterSignature);
    }

    internal static StrongNameKeys Create(
        StrongNameProvider providerOpt,
        string keyFilePath,
        string keyContainerName,
        bool hasCounterSignature) {
        if (string.IsNullOrEmpty(keyFilePath) && string.IsNullOrEmpty(keyContainerName))
            return None;

        if (providerOpt is not null)
            return providerOpt.CreateKeys(keyFilePath, keyContainerName, hasCounterSignature);

        // TODO message
        // var diagnostic = GetError(keyFilePath, keyContainerName, new CodeAnalysisResourcesLocalizableErrorArgument(nameof(CodeAnalysisResources.AssemblySigningNotSupported)), messageProvider);
        var diagnostic = GetError(keyFilePath, keyContainerName, null);
        return new StrongNameKeys(diagnostic);
    }

    internal bool canSign => !keyPair.IsDefault || keyContainer is not null;

    internal bool canProvideStrongName => canSign || !publicKey.IsDefault;

    internal static BelteDiagnostic GetError(string keyFilePath, string keyContainerName, object message) {
        if (keyContainerName is not null)
            return GetContainerError(keyContainerName, message);
        else
            return GetKeyFileError(keyFilePath, message);
    }

    internal static BelteDiagnostic GetContainerError(string name, object message) {
        // TODO
        return null;
        // return messageProvider.CreateDiagnostic(messageProvider.ERR_PublicKeyContainerFailure, Location.None, name, message);
    }

    internal static BelteDiagnostic GetKeyFileError(string path, object message) {
        // TODO
        return null;
        // return messageProvider.CreateDiagnostic(messageProvider.ERR_PublicKeyFileFailure, Location.None, path, message);
    }

    internal static bool IsValidPublicKeyString(string? publicKey) {
        if (string.IsNullOrEmpty(publicKey) || publicKey.Length % 2 != 0)
            return false;

        foreach (var c in publicKey) {
            if (!(c >= '0' && c <= '9') &&
                !(c >= 'a' && c <= 'f') &&
                !(c >= 'A' && c <= 'F')) {
                return false;
            }
        }

        return true;
    }
}
