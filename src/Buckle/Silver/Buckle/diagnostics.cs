using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle {

    public enum DiagnosticType {
        error,
        warning,
        fatal,
        unknown,
    }

    public class Diagnostic {
        public DiagnosticType type { get; }
        public string msg { get; }
        public TextSpan? span { get; }

        public Diagnostic(DiagnosticType type_, TextSpan? span_, string msg_) {
            type = type_;
            msg = msg_;
            span = span_;
        }
    }

    public class DiagnosticQueue {
        private List<Diagnostic> diagnostics_;
        public int count => diagnostics_.Count;
        public bool Any() => diagnostics_.Any();
        public Diagnostic[] ToArray() => diagnostics_.ToArray();
        public IEnumerator GetEnumerator() => diagnostics_.GetEnumerator();

        public DiagnosticQueue() {
            diagnostics_ = new List<Diagnostic>();
        }

        public void Push(DiagnosticType type, TextSpan? span, string msg) {
            diagnostics_.Add(new Diagnostic(type, span, msg));
        }

        public void Push(TextSpan? span, string msg) { Push(DiagnosticType.error, span, msg); }
        public void Push(DiagnosticType type, string msg) { Push(type, null, msg); }
        public void Push(string msg) { Push(DiagnosticType.error, null, msg); }
        public void Push(Diagnostic diagnostic) { diagnostics_.Add(diagnostic); }

        public void Move(DiagnosticQueue diagnosticQueue) {
            Diagnostic diagnostic = diagnosticQueue.Pop();
            while (diagnostic != null) {
                diagnostics_.Add(diagnostic);
                diagnostic = diagnosticQueue.Pop();
            }
        }

        public Diagnostic? Pop() {
            if (diagnostics_.Count == 0) return null;
            Diagnostic diagnostic = diagnostics_[0];
            diagnostics_.RemoveAt(0);
            return diagnostic;
        }

        public void Clear() {
            diagnostics_.Clear();
        }
    }
}
