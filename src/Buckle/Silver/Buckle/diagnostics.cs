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

    public sealed class Diagnostic {
        public DiagnosticType type { get; }
        public string msg { get; }
        public TextSpan? span { get; }

        /// <summary>
        /// A diagnostic message with a specific location
        /// </summary>
        /// <param name="type_">Severity of diagnostic</param>
        /// <param name="span_">Location of the diagnostic</param>
        /// <param name="msg_">Message/info on the diagnostic</param>
        public Diagnostic(DiagnosticType type_, TextSpan? span_, string msg_) {
            type = type_;
            msg = msg_;
            span = span_;
        }
    }

    public sealed class DiagnosticQueue {
        internal List<Diagnostic> diagnostics_;
        public int count => diagnostics_.Count;
        public bool Any() => diagnostics_.Any();
        public Diagnostic[] ToArray() => diagnostics_.ToArray();
        public IEnumerator GetEnumerator() => diagnostics_.GetEnumerator();

        /// <summary>
        /// Queue structure for organizing diagnostics
        /// </summary>
        public DiagnosticQueue() {
            diagnostics_ = new List<Diagnostic>();
        }

        /// <summary>
        /// Pushes a diagnostic onto the Queue
        /// </summary>
        /// <param name="diagnostic">Diagnostic to copy onto the queue</param>
        public void Push(Diagnostic diagnostic) { diagnostics_.Add(diagnostic); }
        public void Push(DiagnosticType type, TextSpan? span, string msg) { Push(new Diagnostic(type, span, msg)); }
        public void Push(TextSpan? span, string msg) { Push(DiagnosticType.error, span, msg); }
        public void Push(DiagnosticType type, string msg) { Push(type, null, msg); }
        public void Push(string msg) { Push(DiagnosticType.error, null, msg); }

        /// <summary>
        /// Pops all diagnostics off queue and pushes them onto this
        /// </summary>
        /// <param name="diagnosticQueue">queue to pop and copy from</param>
        public void Move(DiagnosticQueue diagnosticQueue) {
            Diagnostic diagnostic = diagnosticQueue.Pop();
            while (diagnostic != null) {
                diagnostics_.Add(diagnostic);
                diagnostic = diagnosticQueue.Pop();
            }
        }

        /// <summary>
        /// Removes a diagnostic
        /// </summary>
        /// <returns>first diagnostic on the queue</returns>
        public Diagnostic? Pop() {
            if (diagnostics_.Count == 0) return null;
            Diagnostic diagnostic = diagnostics_[0];
            diagnostics_.RemoveAt(0);
            return diagnostic;
        }

        /// <summary>
        /// Removes all diagnostics
        /// </summary>
        public void Clear() {
            diagnostics_.Clear();
        }
    }
}