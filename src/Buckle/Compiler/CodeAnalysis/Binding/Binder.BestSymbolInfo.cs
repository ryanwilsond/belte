
namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private readonly struct BestSymbolInfo {
        private readonly BestSymbolLocation _location;
        private readonly int _index;

        internal BestSymbolInfo(BestSymbolLocation location, int index) {
            _location = location;
            _index = index;
        }

        internal int index => isNone ? -1 : _index;

        internal bool isFromSourceModule => _location == BestSymbolLocation.FromSourceModule;

        internal bool isFromAddedModule => _location == BestSymbolLocation.FromAddedModule;

        internal bool isFromCompilation
            => (_location == BestSymbolLocation.FromSourceModule) || (_location == BestSymbolLocation.FromAddedModule);

        internal bool isNone => _location == BestSymbolLocation.None;

        internal bool isFromCorLibrary => _location == BestSymbolLocation.FromCorLibrary;

        internal static bool Sort(ref BestSymbolInfo first, ref BestSymbolInfo second) {
            if (IsSecondLocationBetter(first._location, second._location)) {
                (second, first) = (first, second);
                return true;
            }

            return false;
        }

        internal static bool IsSecondLocationBetter(BestSymbolLocation first, BestSymbolLocation second) {
            return (first == BestSymbolLocation.None) || (first > second);
        }
    }
}
