using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed partial class SingleTypeDeclaration {
    internal readonly struct TypeDeclarationIdentity : IEquatable<TypeDeclarationIdentity> {
        private readonly SingleTypeDeclaration _decl;

        internal TypeDeclarationIdentity(SingleTypeDeclaration decl) {
            _decl = decl;
        }

        public override bool Equals(object obj) {
            return obj is TypeDeclarationIdentity identity1 && Equals(identity1);
        }

        public bool Equals(TypeDeclarationIdentity other) {
            var thisDecl = _decl;
            var otherDecl = other._decl;

            if ((object)thisDecl == otherDecl)
                return true;

            if ((thisDecl._arity != otherDecl._arity) ||
                (thisDecl.kind != otherDecl.kind) ||
                (thisDecl.name != otherDecl.name)) {
                return false;
            }

            return true;
        }

        public override int GetHashCode() {
            var thisDecl = _decl;
            return Hash.Combine(thisDecl.name.GetHashCode(),
                Hash.Combine(thisDecl.arity.GetHashCode(),
                (int)thisDecl.kind));
        }
    }
}
