using System.Text.RegularExpressions;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class GeneratedNameParser {
    private static readonly string RegexPatternString = $@"<([a-zA-Z_0-9]*)>F([0-9A-F]{{{Sha256LengthHexChars}}})__";
    private static readonly Regex FileTypeOrdinalPattern = new Regex(RegexPatternString, RegexOptions.Compiled);

    private const int Sha256LengthBytes = 32;
    private const int Sha256LengthHexChars = Sha256LengthBytes * 2;

    internal const char FileTypeNameStartChar = '<';

    internal static bool TryParseFileTypeName(
        string generatedName,
        out string displayFileName,
        out byte[] checksum,
        out string originalTypeName) {
        if (FileTypeOrdinalPattern.Match(generatedName) is Match {
            Success: true, Groups: var groups, Index: var index, Length: var length
        }) {
            displayFileName = groups[1].Value;

            var checksumString = groups[2].Value;
            var builder = new byte[Sha256LengthBytes];

            for (var i = 0; i < Sha256LengthBytes; i++) {
                builder[i] = (byte)((HexCharToByte(checksumString[i * 2]) << 4) |
                    HexCharToByte(checksumString[i * 2 + 1]));
            }

            checksum = builder;

            var prefixEndsAt = index + length;
            originalTypeName = generatedName.Substring(prefixEndsAt);
            return true;
        }

        checksum = null;
        displayFileName = null;
        originalTypeName = null;
        return false;

        static byte HexCharToByte(char c) {
            return c switch {
                >= '0' and <= '9' => (byte)(c - '0'),
                >= 'A' and <= 'F' => (byte)(10 + c - 'A'),
                _ => Throw(c)
            };

            static byte Throw(char c) => throw ExceptionUtilities.UnexpectedValue(c);
        }
    }
}
