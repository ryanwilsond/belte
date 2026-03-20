using System;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    private readonly struct ImportInfo : IEquatable<ImportInfo> {
        internal readonly SyntaxTree tree;
        internal readonly SyntaxKind kind;
        internal readonly TextSpan span;

        internal ImportInfo(SyntaxTree tree, SyntaxKind kind, TextSpan span) {
            this.tree = tree;
            this.kind = kind;
            this.span = span;
        }

        public bool Equals(ImportInfo other) {
            return other.kind == kind &&
                   other.tree == tree &&
                   other.span == span;
        }

        public override bool Equals(object? obj) {
            return (obj is ImportInfo info) && Equals(info);
        }

        public override int GetHashCode() {
            return Hash.Combine(tree, span.start);
        }
    }
}
