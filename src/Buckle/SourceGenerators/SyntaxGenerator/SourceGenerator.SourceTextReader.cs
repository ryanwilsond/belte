
#if NETSTANDARD

using System;
using System.IO;
using Microsoft.CodeAnalysis.Text;

namespace SyntaxGenerator;

public sealed partial class SourceGenerator {
    private sealed class SourceTextReader : TextReader {
        private readonly SourceText _sourceText;
        private int _position;

        public SourceTextReader(SourceText sourceText) {
            _sourceText = sourceText;
            _position = 0;
        }

        public override int Peek() {
            if (_position == _sourceText.Length) {
                return -1;
            }

            return _sourceText[_position];
        }

        public override int Read() {
            if (_position == _sourceText.Length) {
                return -1;
            }

            return _sourceText[_position++];
        }

        public override int Read(char[] buffer, int index, int count) {
            var charsToCopy = Math.Min(count, _sourceText.Length - _position);
            _sourceText.CopyTo(_position, buffer, index, charsToCopy);
            _position += charsToCopy;
            return charsToCopy;
        }
    }
}

#endif
