using System;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BinderFactory {
    internal readonly struct BinderCacheKey : IEquatable<BinderCacheKey> {
        internal readonly SyntaxNode syntaxNode;
        internal readonly NodeUsage nodeUsage;

        internal BinderCacheKey(SyntaxNode syntaxNode, NodeUsage nodeUsage) {
            this.syntaxNode = syntaxNode;
            this.nodeUsage = nodeUsage;
        }

        bool IEquatable<BinderCacheKey>.Equals(BinderCacheKey other) {
            return syntaxNode == other.syntaxNode && nodeUsage == other.nodeUsage;
        }

        public override int GetHashCode() {
            return Hash.Combine(syntaxNode.GetHashCode(), (int)nodeUsage);
        }

        public override bool Equals(object obj) {
            throw new NotSupportedException();
        }
    }
}
