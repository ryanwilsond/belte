using System;
using System.IO;
using System.Text;

namespace Repl;

public abstract partial class Repl {
    /// <summary>
    /// Wrapper around the System.Console class.
    /// </summary>
    internal sealed class OutputCapture : TextWriter, IDisposable {
        /// <summary>
        /// Creates an out.
        /// </summary>
        internal OutputCapture() {
            // captured = new List<List<string>>();
        }

        // internal List<List<string>> captured { get; private set; }

        /// <summary>
        /// Encoding to use, constant.
        /// </summary>
        /// <value>Ascii.</value>
        public override Encoding Encoding { get { return Encoding.ASCII; } }

        public override void Write(string output) {
            Console.Write(output);
        }

        public override void WriteLine(string output) {
            Console.WriteLine(output);
        }

        public override void WriteLine() {
            Console.WriteLine();
        }

        /// <summary>
        /// Changes Console cursor position.
        /// </summary>
        /// <param name="left">Column position (left (0) -> right).</param>
        /// <param name="top">Row position (top (0) -> down).</param>
        public void SetCursorPosition(int left, int top) {
            Console.SetCursorPosition(left, top);
        }
    }
}
