
namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMethodSymbol {
    // TODO The reason I'm using an enum and a class is because eventually this will include more complex information
    private protected class BehaviorSpecifierInfo {
        internal static readonly BehaviorSpecifierInfo Default = new BehaviorSpecifierInfo(BehaviorSpecifiers.None);

        private readonly BehaviorSpecifiers _behaviorSpecifiers;

        internal BehaviorSpecifierInfo(BehaviorSpecifiers behaviorSpecifiers) {
            _behaviorSpecifiers = behaviorSpecifiers;
        }

        internal bool isPure => (_behaviorSpecifiers & BehaviorSpecifiers.Pure) != 0;

        internal bool isNoThrow => (_behaviorSpecifiers & BehaviorSpecifiers.NoThrow) != 0;

        internal bool isNoAlloc => (_behaviorSpecifiers & BehaviorSpecifiers.NoAlloc) != 0;

        internal bool shouldMemoizeIfPure => (_behaviorSpecifiers & BehaviorSpecifiers.Memoize) != 0;
    }
}
