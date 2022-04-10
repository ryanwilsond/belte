using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding {
    internal sealed class Cast {
        public bool exists { get; }
        public bool isIdentity { get; }
        public bool isImplicit { get; }
        public bool isExplicit => exists && !isImplicit;

        public static readonly Cast None = new Cast(false, false, false);
        public static readonly Cast Identity = new Cast(true, true, true);
        public static readonly Cast Implicit = new Cast(true, false, true);
        public static readonly Cast Explicit = new Cast(true, false, false);

        private Cast(bool exists_, bool isIdentity_, bool isImplicit_) {
            exists = exists_;
            isIdentity = isIdentity_;
            isImplicit = isImplicit_;
        }

        public static Cast Classify(TypeSymbol from, TypeSymbol to) {
            if (from == to)
                return Cast.Identity;

            if (from != TypeSymbol.Void && to == TypeSymbol.Any)
                return Cast.Implicit;
            if (from == TypeSymbol.Any && to != TypeSymbol.Void)
                return Cast.Explicit;
            if (from == TypeSymbol.Bool || from == TypeSymbol.Int)
                if (to == TypeSymbol.String)
                    return Cast.Explicit;
            if (from == TypeSymbol.String)
                if (to == TypeSymbol.Bool || to == TypeSymbol.Int)
                    return Cast.Explicit;

            return Cast.None;
        }
    }
}
