using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Repl;

public abstract partial class Repl {
    private sealed class SubmissionView {
        private readonly LineRenderHandler _lineRenderer;
        private readonly ObservableCollection<string> _document;
        private int _cursorTop;
        private int _renderedLineCount;
        private int _currentLine;
        private int _currentCharacter;
        private OutputCapture _writer;

        internal SubmissionView(
            LineRenderHandler lineRenderer, ObservableCollection<string> document, OutputCapture writer) {
            _lineRenderer = lineRenderer;
            _document = document;
            _document.CollectionChanged += SubmissionDocumentChanged;
            _cursorTop = Console.CursorTop;
            _writer = writer;
            Render();
        }

        internal int currentLine {
            get => _currentLine;
            set {
                if (_currentLine != value) {
                    _currentLine = value;
                    _currentCharacter = Math.Min(_document[_currentLine].Length, _currentCharacter);
                    UpdateCursorPosition();
                }
            }
        }

        internal int currentCharacter {
            get => _currentCharacter;
            set {
                if (_currentCharacter != value) {
                    _currentCharacter = value;
                    UpdateCursorPosition();
                }
            }
        }

        internal Stack<(char, int)> currentBlockTabbing = new Stack<(char, int)>();
        internal int currentTypingTabbing = 0;

        private void SubmissionDocumentChanged(object sender, NotifyCollectionChangedEventArgs e) {
            Render();
        }

        private void Render() {
            try {
                Console.CursorVisible = false;
                var lineCount = 0;

                foreach (var line in _document) {
                    if (_cursorTop + lineCount >= Console.WindowHeight - 1) {
                        _writer.SetCursorPosition(0, Console.WindowHeight - 1);
                        _writer.WriteLine();

                        if (_cursorTop > 0)
                            _cursorTop--;
                    }

                    _writer.SetCursorPosition(0, _cursorTop + lineCount);
                    var previous = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;

                    if (lineCount == 0)
                        _writer.Write("» ");
                    else
                        _writer.Write("· ");

                    Console.ForegroundColor = previous;
                    _lineRenderer(_document, lineCount);
                    _writer.Write(new string(' ', Console.WindowWidth - line.Length - 2));
                    lineCount++;
                }

                var blankLineCount = _renderedLineCount - lineCount;

                if (blankLineCount > 0) {
                    var blankLine = new string(' ', Console.WindowWidth);

                    for (var i = 0; i < blankLineCount; i++) {
                        _writer.SetCursorPosition(0, _cursorTop + lineCount + i);
                        _writer.WriteLine(blankLine);
                    }
                }

                _renderedLineCount = lineCount;
                UpdateCursorPosition();
            } finally {
                // Mostly a quality of life improvement
                // Makes it so you are not left without a cursor if the program crashes
                Console.CursorVisible = true;
            }
        }

        private void UpdateCursorPosition() {
            _writer.SetCursorPosition(2 + _currentCharacter, _cursorTop + _currentLine);
        }
    }
}
