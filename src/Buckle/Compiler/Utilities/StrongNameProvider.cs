
namespace Buckle.Utilities;

internal abstract class StrongNameProvider {
    private protected StrongNameProvider() { }

    // internal abstract StrongNameFileSystem FileSystem { get; }

    // internal abstract void SignFile(StrongNameKeys keys, string filePath);

    // internal abstract void SignBuilder(ExtendedPEBuilder peBuilder, BlobBuilder peBlob, RSAParameters privateKey);

    internal abstract StrongNameKeys CreateKeys(string keyFilePath, string keyContainerName, bool hasCounterSignature);

    public abstract override int GetHashCode();

    public abstract override bool Equals(object? other);
}
