using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private sealed class BinderWithContainingMember : Binder {
        internal BinderWithContainingMember(Binder next, BinderFlags flags, Symbol containingMember)
            : base(next, flags) {
            this.containingMember = containingMember;
        }

        internal BinderWithContainingMember(Binder next, Symbol containingMember) : base(next) {
            this.containingMember = containingMember;
        }

        internal override Symbol containingMember { get; }
    }
}
