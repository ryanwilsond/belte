using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal partial struct MetadataTypeName {
    public readonly struct Key : IEquatable<Key> {
        private readonly string _namespaceOrFullyQualifiedName;
        private readonly string _typeName;
        private readonly byte _useCLSCompliantNameArityEncoding;
        private readonly short _forcedArity;

        internal Key(in MetadataTypeName mdTypeName) {
            if (mdTypeName.isNull) {
                _namespaceOrFullyQualifiedName = null;
                _typeName = null;
                _useCLSCompliantNameArityEncoding = 0;
                _forcedArity = 0;
            } else {
                if (mdTypeName._fullName != null) {
                    _namespaceOrFullyQualifiedName = mdTypeName._fullName;
                    _typeName = null;
                } else {
                    _namespaceOrFullyQualifiedName = mdTypeName._namespaceName;
                    _typeName = mdTypeName._typeName;
                }

                _useCLSCompliantNameArityEncoding = mdTypeName.useCLSCompliantNameArityEncoding ? (byte)1 : (byte)0;
                _forcedArity = mdTypeName._forcedArity;
            }
        }

        private bool _hasFullyQualifiedName => _typeName is null;

        public bool Equals(Key other) {
            return _useCLSCompliantNameArityEncoding == other._useCLSCompliantNameArityEncoding &&
                _forcedArity == other._forcedArity &&
                EqualNames(ref other);
        }

        private bool EqualNames(ref Key other) {
            if (_typeName == other._typeName) {
                return _namespaceOrFullyQualifiedName == other._namespaceOrFullyQualifiedName;
            }

            if (_hasFullyQualifiedName) {
                return MetadataHelpers.SplitNameEqualsFullyQualifiedName(
                    other._namespaceOrFullyQualifiedName,
                    other._typeName,
                    _namespaceOrFullyQualifiedName
                );
            }

            if (other._hasFullyQualifiedName) {
                return MetadataHelpers.SplitNameEqualsFullyQualifiedName(
                    _namespaceOrFullyQualifiedName,
                    _typeName,
                    other._namespaceOrFullyQualifiedName
                );
            }

            return false;
        }

        public override bool Equals(object obj) {
            return obj is Key key && Equals(key);
        }

        public override int GetHashCode() {
            return Hash.Combine(GetHashCodeName(),
                   Hash.Combine(_useCLSCompliantNameArityEncoding != 0, _forcedArity));
        }

        private int GetHashCodeName() {
            var hashCode = Hash.GetFNVHashCode(_namespaceOrFullyQualifiedName);

            if (!_hasFullyQualifiedName) {
                hashCode = Hash.CombineFNVHash(hashCode, MetadataHelpers.DotDelimiter);
                hashCode = Hash.CombineFNVHash(hashCode, _typeName);
            }

            return hashCode;
        }
    }
}
