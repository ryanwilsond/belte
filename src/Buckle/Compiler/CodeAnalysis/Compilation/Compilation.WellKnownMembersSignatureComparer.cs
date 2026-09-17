using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed class WellKnownMembersSignatureComparer : SpecialMembersSignatureComparer {
        private readonly Compilation _compilation;

        internal WellKnownMembersSignatureComparer(Compilation compilation) {
            _compilation = compilation;
        }

        private protected override bool MatchTypeToTypeId(TypeSymbol type, int typeId) {
            var wellKnownId = (WellKnownType)typeId;

            if (wellKnownId.IsWellKnownType())
                return type.Equals(_compilation.GetWellKnownType(wellKnownId), TypeCompareKind.ConsiderEverything);

            return base.MatchTypeToTypeId(type, typeId);
        }
    }
}
