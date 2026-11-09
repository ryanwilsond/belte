using System;
using System.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public sealed class SyntaxAnnotation : IEquatable<SyntaxAnnotation?> {
    public static SyntaxAnnotation ElasticAnnotation { get; } = new SyntaxAnnotation();

    private readonly long _id;
    private static long NextId;

    public string kind { get; }
    public object data { get; }

    public SyntaxAnnotation() {
        _id = System.Threading.Interlocked.Increment(ref NextId);
    }

    public SyntaxAnnotation(string kind)
        : this() {
        this.kind = kind;
    }

    public SyntaxAnnotation(string kind, object data)
        : this(kind) {
        this.data = data;
    }

    private string GetDebuggerDisplay() {
        return string.Format("Annotation: Kind='{0}' Data='{1}'", kind ?? "", data ?? "");
    }

    public bool Equals(SyntaxAnnotation other) {
        return other is not null && _id == other._id;
    }

    public static bool operator ==(SyntaxAnnotation left, SyntaxAnnotation right) {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(SyntaxAnnotation left, SyntaxAnnotation right) {
        return !(left == right);
    }

    public override bool Equals(object obj) {
        return Equals(obj as SyntaxAnnotation);
    }

    public override int GetHashCode() {
        return _id.GetHashCode();
    }
}
