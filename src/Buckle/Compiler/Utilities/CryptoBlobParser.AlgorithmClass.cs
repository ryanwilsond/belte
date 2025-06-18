
namespace Buckle.Utilities;

internal static partial class CryptoBlobParser {
    private enum AlgorithmClass : byte {
        Signature = 1,
        Hash = 4,
    }
}
