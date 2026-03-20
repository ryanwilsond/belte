using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries;

internal readonly struct SpecialOrKnownType {
    internal static SpecialOrKnownType Unset = new SpecialOrKnownType();

    private SpecialOrKnownType(SpecialType specialType) {
        this.specialType = specialType;
        knownType = CorLibrary.GetSpecialType(specialType);
    }

    private SpecialOrKnownType(TypeSymbol knownType) {
        this.knownType = knownType;
    }

    internal SpecialType specialType { get; }

    internal TypeSymbol knownType { get; }

    public static implicit operator SpecialOrKnownType(SpecialType specialType) {
        return new SpecialOrKnownType(specialType);
    }

    public static implicit operator SpecialOrKnownType(TypeSymbol knownType) {
        return new SpecialOrKnownType(knownType);
    }

    internal sealed class Boxed {
        internal Boxed(SpecialOrKnownType type) {
            this.type = type;
        }

        internal readonly SpecialOrKnownType type;
    }
}
