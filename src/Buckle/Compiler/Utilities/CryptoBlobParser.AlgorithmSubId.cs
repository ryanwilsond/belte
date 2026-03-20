
namespace Buckle.Utilities;

internal static partial class CryptoBlobParser {
    private enum AlgorithmSubId : byte {
        Sha1Hash = 4,
        MacHash = 5,
        RipeMdHash = 6,
        RipeMd160Hash = 7,
        Ssl3ShaMD5Hash = 8,
        HmacHash = 9,
        Tls1PrfHash = 10,
        HashReplacOwfHash = 11,
        Sha256Hash = 12,
        Sha384Hash = 13,
        Sha512Hash = 14,
    }
}
