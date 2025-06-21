namespace Buckle.Utilities;

internal static partial class CryptoBlobParser {
    private struct AlgorithmId {
        private const int AlgorithmClassOffset = 13;
        private const int AlgorithmClassMask = 0x7;
        private const int AlgorithmSubIdOffset = 0;
        private const int AlgorithmSubIdMask = 0x1ff;

        private readonly uint _flags;

        internal const int RsaSign = 0x00002400;
        internal const int Sha = 0x00008004;

        internal AlgorithmId(uint flags) {
            _flags = flags;
        }

        internal bool isSet => _flags != 0;

        internal AlgorithmClass @class => (AlgorithmClass)((_flags >> AlgorithmClassOffset) & AlgorithmClassMask);

        internal AlgorithmSubId subId => (AlgorithmSubId)((_flags >> AlgorithmSubIdOffset) & AlgorithmSubIdMask);
    }
}
