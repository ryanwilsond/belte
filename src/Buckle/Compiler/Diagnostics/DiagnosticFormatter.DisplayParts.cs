using System;
using System.Collections.Generic;
using System.Text;

namespace Buckle.Diagnostics;

public static partial class DiagnosticFormatter {
    private readonly struct DisplayParts {
        private readonly List<(string text, ConsoleColor color)> _parts;

        public DisplayParts() {
            _parts = [];
        }

        public override string ToString() {
            var builder = new StringBuilder();

            foreach (var (text, _) in _parts)
                builder.Append(text);

            return builder.ToString();
        }

        internal void Write() {
            var initialColor = Console.ForegroundColor;

            foreach (var (text, color) in _parts) {
                Console.ForegroundColor = color;
                Console.Write(text);
            }

            Console.ForegroundColor = initialColor;
        }

        internal void Add(string text, ConsoleColor color) {
            _parts.Add((text, color));
        }
    }
}
