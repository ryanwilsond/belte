using System;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol {
    private readonly struct LocalTypeInferenceInProgressKey : IEquatable<LocalTypeInferenceInProgressKey> {
        internal readonly SourceDataContainerSymbol local;
        internal readonly SyntaxNode reference;

        internal LocalTypeInferenceInProgressKey(SourceDataContainerSymbol local, SyntaxNode reference) {
            this.local = local;
            this.reference = reference;
        }

        public bool Equals(LocalTypeInferenceInProgressKey other) {
            return local == (object)other.local && reference == other.reference;
        }

        public override bool Equals(object? obj) {
            return obj is LocalTypeInferenceInProgressKey key && Equals(key);
        }

        public override int GetHashCode() {
            return Hash.Combine(RuntimeHelpers.GetHashCode(local), reference.GetHashCode());
        }
    }
}
